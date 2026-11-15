// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Queues a failed or incomplete job again.
/// </summary>
/// <remarks>
/// <para>
/// The job keeps the files that it still holds. A track whose file is gone downloads again.
/// </para>
/// <para>
/// The registry applies the change under its lock, so a second call or a sweep between the check
/// and the change gives <see cref="NotRetryable"/>.
/// </para>
/// <para>
/// Unless the job forces its link, a retry refuses a link that another job covers since the job
/// failed.
/// </para>
/// </remarks>
public static class RetryJob
{
    public sealed record Command(Guid Id);

    public sealed record Retried;

    public readonly union Outcome(Retried, NotFound, NotRetryable, AlreadyGrabbed);

    internal sealed class Handler(
        JobRegistry jobs,
        IAudioSource audio,
        IVideoSource videos,
        IJobStore store,
        JobSignal signal
    ) : ICommandHandler<Command, Outcome>
    {
        public async ValueTask<Outcome> HandleAsync(
            Command command,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(command);

            if (jobs.Get(command.Id) is not { } job)
            {
                return new NotFound();
            }

            if (!job.CanRetry)
            {
                return new NotRetryable();
            }

            if (
                !job.Force
                && jobs.Find(other => other.Id != job.Id && other.Covers(job.Request))
                    is { } previous
            )
            {
                return new AlreadyGrabbed(previous);
            }

            var gone = Gone(job);
            var retried = false;
            var updated = jobs.Update(job.Id, current =>
            {
                retried = current.CanRetry;
                return retried ? current.Retry(gone) : current;
            });
            if (updated is null)
            {
                return new NotFound();
            }

            if (!retried)
            {
                return new NotRetryable();
            }

            await store.SaveAsync(updated, cancellationToken);
            signal.Ring();
            return new Retried();
        }

        private HashSet<VideoId> Gone(Job job) =>
            job.Request switch
            {
                JobRequest.Video video when videos.FindKept(job.Id, video.VideoId) is null =>
                [
                    video.VideoId,
                ],
                JobRequest.Video => [],
                JobRequest.Audio =>
                [
                    .. job
                        .Tracks.Where(track =>
                            track.State is TrackState.Done
                            && !audio.IsDownloaded(job.Id, track.Track.VideoId)
                        )
                        .Select(track => track.Track.VideoId),
                ],
            };
    }
}
