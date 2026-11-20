// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;

namespace YGrab.Tests.Fakes;

/// <summary>
/// Answers each download with the next failure of the script, or with a success once the script
/// is empty.
/// </summary>
internal sealed class FakeVideoSource : IVideoSource
{
    private readonly Queue<RemoteFailure?> _script = new();

    public List<(VideoId VideoId, VideoQuality Quality)> Downloads { get; } = [];

    public Dictionary<JobId, VideoInfo> Kept { get; } = [];

    public void Script(params RemoteFailure?[] answers)
    {
        foreach (var answer in answers)
        {
            _script.Enqueue(answer);
        }
    }

    public VideoInfo? FindKept(JobId jobId, VideoId videoId) => Kept.GetValueOrDefault(jobId);

    public ValueTask<VideoDownload> DownloadAsync(
        JobId jobId,
        VideoId videoId,
        VideoQuality quality,
        Action<double> progress,
        CancellationToken cancellationToken
    )
    {
        Downloads.Add((videoId, quality));
        progress(0.5);
        return _script.TryDequeue(out var failure) && failure is not null
            ? ValueTask.FromResult<VideoDownload>(failure)
            : ValueTask.FromResult<VideoDownload>(Samples.VideoDetails);
    }
}
