// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;
using YGrab.Infrastructure.Configuration;

namespace YGrab.Infrastructure.YtDlp;

/// <summary>
/// Runs one yt-dlp process per track.
/// </summary>
/// <remarks>
/// <para>
/// It keeps the best audio stream as it is: Opus in <c>.opus</c> or AAC in <c>.m4a</c>.
/// </para>
/// <para>
/// yt-dlp writes into the work folder of the job. A finished download moves to the tracks folder,
/// so a file there is always complete.
/// </para>
/// </remarks>
internal sealed class YtDlpAudioSource(YtDlpRunner runner, IOptions<StorageOptions> storage)
    : IAudioSource
{
    internal static readonly TimeSpan TrackTimeout = TimeSpan.FromMinutes(10);

    // The drc formats have their dynamic range compressed
    private const string Format = "ba[format_id!*=drc]/ba";
    private const string FileTag = "file";
    private const string NoFileMessage = "YouTube sent no file.";

    public bool IsDownloaded(Guid jobId, VideoId videoId)
    {
        ArgumentNullException.ThrowIfNull(videoId);

        var directory = storage.Value.TracksDirectory(jobId);
        return Directory.Exists(directory)
            && Directory.EnumerateFiles(directory, $"{videoId.Value}.*").Any();
    }

    public async ValueTask<AudioDownload> DownloadAsync(
        Guid jobId,
        VideoId videoId,
        Action<double> progress,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(videoId);
        ArgumentNullException.ThrowIfNull(progress);

        var work = storage.Value.WorkDirectory(jobId);
        string[] arguments =
        [
            "--format",
            Format,
            "--extract-audio",
            "--output",
            Path.Combine(work, $"{videoId.Value}.%(ext)s"),
            "--progress-template",
            YtDlpProgress.AudioTemplate,
            "--print",
            $"after_move:{FileTag} %(filepath)s",
        ];
        string? path = null;
        var failure = await runner.RunAsync(
            work,
            videoId,
            TrackTimeout,
            arguments,
            line =>
            {
                switch (YtDlpRunner.Split(line))
                {
                    case (FileTag, var file):
                        path = file;
                        break;
                    case var _ when YtDlpProgress.TryParseAudio(line, out var fraction):
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
            return new RemoteFailure.Transient(NoFileMessage);
        }

        Keep(jobId, videoId, path);
        return new Downloaded();
    }

    private void Keep(Guid jobId, VideoId videoId, string path)
    {
        var tracks = Directory.CreateDirectory(storage.Value.TracksDirectory(jobId));
        File.Move(
            path,
            Path.Combine(tracks.FullName, videoId.Value + Path.GetExtension(path)),
            overwrite: true
        );
    }
}
