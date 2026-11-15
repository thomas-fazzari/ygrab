// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;

namespace YGrab.Tests.Fakes;

/// <summary>
/// Answers each download with the next failure of the script, or with a success once the script
/// is empty.
/// </summary>
/// <remarks>
/// A download can wait after it has done its work, and then ignores cancellation, like a step
/// that is past its last cancellation point.
/// </remarks>
internal sealed class FakeAudioSource : IAudioSource
{
    private readonly Queue<RemoteFailure?> _script = new();
    private readonly TaskCompletionSource _waiting = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private TaskCompletionSource? _release;

    public List<VideoId> Downloads { get; } = [];

    /// <summary>
    /// Completes when a download waits for <see cref="Release"/>.
    /// </summary>
    public Task Waiting => _waiting.Task;

    public void Script(params RemoteFailure?[] answers)
    {
        foreach (var answer in answers)
        {
            _script.Enqueue(answer);
        }
    }

    public void WaitAfterNextDownload() =>
        _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _release?.SetResult();

    public bool IsDownloaded(Guid jobId, VideoId videoId) => false;

    public async ValueTask<AudioDownload> DownloadAsync(
        Guid jobId,
        VideoId videoId,
        Action<double> progress,
        CancellationToken cancellationToken
    )
    {
        Downloads.Add(videoId);
        progress(0.5);
        if (_release is { } gate)
        {
            _waiting.TrySetResult();
            await gate.Task;
        }

        return _script.TryDequeue(out var failure) && failure is not null
            ? failure
            : new Downloaded();
    }
}
