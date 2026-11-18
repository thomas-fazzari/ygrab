// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Serialization;
using YGrab.Domain;
using YGrab.Engine.Jobs;
using YGrab.Infrastructure.Configuration;
using YGrab.Infrastructure.Output;

namespace YGrab.Infrastructure.YtDlp;

/// <summary>
/// Runs one yt-dlp process per video, which downloads, merges and embeds in one call.
/// </summary>
/// <remarks>
/// <para>
/// yt-dlp picks the best format within the resolution cap, and ffmpeg only merges or remuxes
/// it. The container is MP4 when the codecs allow it, otherwise MKV.
/// </para>
/// <para>
/// <see cref="VideoQuality.Audio"/> keeps the best audio stream as it is, with the format of
/// <see cref="YtDlpAudioSource"/>.
/// </para>
/// <para>
/// yt-dlp embeds the metadata, the thumbnail and the chapters, and prints the title and the
/// channel. A video that yt-dlp reports no title for gets its id as title.
/// </para>
/// <para>
/// yt-dlp writes into the work folder of the job, which is deleted after each run. The tracks
/// folder gets <see cref="InfoFileName"/> first and the video file after it, through
/// <see cref="TrackFiles"/>, so a video file there always has its info.
/// </para>
/// <para>
/// A live stream that is still running ends with exit code 0 and no file, and gives an
/// unavailable failure.
/// </para>
/// </remarks>
internal sealed class YtDlpVideoSource(YtDlpRunner runner, IOptions<StorageOptions> storage)
    : IVideoSource
{
    internal const string LiveMessage = "The video is live now. Grab it once the stream ends.";
    internal const string InfoFileName = "video.json";

    internal static readonly TimeSpan VideoTimeout = TimeSpan.FromHours(3);

    // The drc formats have their dynamic range compressed
    private const string Format = "bv*+ba[format_id!*=drc]/bv*+ba/b";
    private const string MergeFormats = "mp4/mkv";
    private const string RemuxFormats = "mp4>mp4/mkv";
    private const string ThumbnailFormat = "jpg";
    private const string ProgressDelta = "1";
    private const string LiveFilter = "!is_live";

    // The resolution is the smaller dimension, so a vertical short has its resolution cap too
    private const string SortP720 = "res:720,fps,ext";
    private const string SortP1080 = "res:1080,fps,ext";
    private const string SortP1440 = "res:1440,fps,ext";
    private const string SortP2160 = "res:2160,fps,ext";
    private const string SortBest = "res,fps,ext";

    private const string InfoTag = "info";
    private const string LiveTag = "live";
    private const string LiveTrue = "True";

    public VideoInfo? FindKept(Guid jobId, VideoId videoId)
    {
        ArgumentNullException.ThrowIfNull(videoId);

        var infoPath = Path.Combine(storage.Value.TracksDirectory(jobId), InfoFileName);
        if (!File.Exists(infoPath))
        {
            return null;
        }

        return storage.Value.FindTrackFile(jobId, videoId) is null
            ? null
            : ParseInfo(File.ReadAllText(infoPath), videoId);
    }

    public async ValueTask<VideoDownload> DownloadAsync(
        Guid jobId,
        VideoId videoId,
        VideoQuality quality,
        Action<double> progress,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(videoId);
        ArgumentNullException.ThrowIfNull(progress);

        var work = storage.Value.WorkDirectory(jobId);
        var audioOnly = quality is VideoQuality.Audio;
        string[] arguments =
        [
            "--match-filters",
            LiveFilter,
            .. audioOnly ? YtDlpAudioSource.FormatArguments : VideoFormatArguments(quality),
            "--embed-metadata",
            "--embed-chapters",
            "--embed-thumbnail",
            "--convert-thumbnails",
            ThumbnailFormat,
            "--progress-delta",
            ProgressDelta,
            "--progress-template",
            audioOnly ? YtDlpProgress.AudioTemplate : YtDlpProgress.VideoTemplate,
            "--print",
            $"pre_process:{LiveTag} %(is_live)s",
            "--print",
            $"after_move:{InfoTag} %(.{{title,channel}})j",
        ];
        string? path = null;
        string? info = null;
        var live = false;
        try
        {
            var failure = await runner.RunAsync(
                work,
                videoId,
                VideoTimeout,
                arguments,
                line =>
                {
                    switch (YtDlpRunner.Split(line))
                    {
                        case (YtDlpRunner.FileTag, var file):
                            path = file;
                            break;
                        case (InfoTag, var json):
                            info = json;
                            break;
                        case (LiveTag, var isLive):
                            live = isLive is LiveTrue;
                            break;
                        case var _ when TryParseProgress(line, audioOnly, out var fraction):
                            progress(fraction);
                            break;
                    }
                },
                cancellationToken
            );
            if (failure is not null)
            {
                return failure;
            }

            if (path is null)
            {
                return live
                    ? new RemoteFailure.Unavailable(LiveMessage)
                    : new RemoteFailure.Transient(YtDlpRunner.NoFileMessage);
            }

            var video = info is null ? Untitled(videoId) : ParseInfo(info, videoId);
            await KeepAsync(jobId, videoId, video, path, cancellationToken);
            return video;
        }
        finally
        {
            runner.DeleteWork(work);
        }
    }

    private static string[] VideoFormatArguments(VideoQuality quality) =>
        [
            "--format",
            Format,
            "--format-sort",
            FormatSort(quality),
            "--merge-output-format",
            MergeFormats,
            "--remux-video",
            RemuxFormats,
        ];

    private static string FormatSort(VideoQuality quality) =>
        quality switch
        {
            VideoQuality.P720 => SortP720,
            VideoQuality.P1080 => SortP1080,
            VideoQuality.P1440 => SortP1440,
            VideoQuality.P2160 => SortP2160,
            VideoQuality.Best => SortBest,
            _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, message: null),
        };

    private static bool TryParseProgress(string line, bool audioOnly, out double fraction) =>
        audioOnly
            ? YtDlpProgress.TryParseAudio(line, out fraction)
            : YtDlpProgress.TryParseVideo(line, out fraction);

    private async ValueTask KeepAsync(
        Guid jobId,
        VideoId videoId,
        VideoInfo video,
        string path,
        CancellationToken cancellationToken
    )
    {
        var tracks = Directory.CreateDirectory(storage.Value.TracksDirectory(jobId));
        await File.WriteAllTextAsync(
            Path.Combine(tracks.FullName, InfoFileName),
            JsonSerializer.Serialize(
                new VideoDocument(video.Title, video.Channel),
                VideoJson.Default.VideoDocument
            ),
            cancellationToken
        );
        storage.Value.KeepTrackFile(jobId, videoId, path);
    }

    private static VideoInfo Untitled(VideoId videoId) => new(videoId.Value, Channel: null);

    private static VideoInfo ParseInfo(string json, VideoId videoId)
    {
        try
        {
            var document = JsonSerializer.Deserialize(json, VideoJson.Default.VideoDocument);
            return string.IsNullOrWhiteSpace(document?.Title)
                ? Untitled(videoId)
                : new VideoInfo(document.Title, document.Channel);
        }
        catch (JsonException)
        {
            return Untitled(videoId);
        }
    }
}

internal sealed record VideoDocument(string Title, string? Channel);

/// <summary>
/// The JSON of <see cref="YtDlpVideoSource.InfoFileName"/> and of the info line of yt-dlp.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(VideoDocument))]
internal sealed partial class VideoJson : JsonSerializerContext;
