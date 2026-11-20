// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;
using YGrab.Infrastructure.Configuration;
using YGrab.Infrastructure.Output;

namespace YGrab.Infrastructure.YtDlp;

/// <summary>
/// Runs one yt-dlp process per track.
/// </summary>
/// <remarks>
/// <para>
/// It keeps the best audio stream as it is: Opus in <c>.opus</c> or AAC in <c>.m4a</c>.
/// </para>
/// <para>
/// yt-dlp writes into the work folder of the job, which is deleted after each run. A finished
/// download moves to the tracks folder first, through <see cref="TrackFiles"/>.
/// </para>
/// </remarks>
internal sealed class YtDlpAudioSource(YtDlpRunner runner, IOptions<StorageOptions> storage)
    : IAudioSource
{
    internal static readonly TimeSpan TrackTimeout = TimeSpan.FromMinutes(10);

    // The drc formats have their dynamic range compressed
    internal static readonly string[] FormatArguments =
    [
        "--format",
        "ba[format_id!*=drc]/ba",
        "--extract-audio",
    ];

    public bool IsDownloaded(JobId jobId, VideoId videoId)
    {
        ArgumentNullException.ThrowIfNull(videoId);

        return storage.Value.FindTrackFile(jobId, videoId) is not null;
    }

    public async ValueTask<AudioDownload> DownloadAsync(
        JobId jobId,
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
            .. FormatArguments,
            "--progress-template",
            YtDlpProgress.AudioTemplate,
        ];
        string? path = null;
        try
        {
            var failure = await runner.RunAsync(
                work,
                videoId,
                TrackTimeout,
                arguments,
                line =>
                {
                    switch (YtDlpRunner.Split(line))
                    {
                        case (YtDlpRunner.FileTag, var file):
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
                return failure;

            if (path is null)
                return new RemoteFailure.Transient(YtDlpRunner.NoFileMessage);

            storage.Value.KeepTrackFile(jobId, videoId, path);
            return new Downloaded();
        }
        finally
        {
            runner.DeleteWork(work);
        }
    }
}
