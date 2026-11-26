// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Logging;
using YGrab.Domain;
using YGrab.Engine.Diagnostics;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Moves the current job one step ahead: resolve it, download one track or one video, or package it.
/// </summary>
/// <remarks>
/// <para>
/// A video job never resolves: one download gives the video file and its title.
/// </para>
/// <para>
/// A call runs at most one network step and never waits. It tells the caller how long to wait
/// before the next call, so that the caller owns every wait.
/// </para>
/// <para>
/// A rate limit starts the <see cref="Cooldown"/>, and only a packaged job clears it. A ring ends
/// the wait of the cooldown, so a client that ends the cooldown resumes the jobs.
/// </para>
/// <para>
/// The provider of the extra tags is not YouTube, so its failures change neither the cooldown nor
/// the retries of the job.
/// </para>
/// <para>
/// A job that a client removes during its step stops the step, which then ends with the same pause
/// as any step, and the files that the step left are deleted.
/// </para>
/// </remarks>
public static partial class AdvanceJobs
{
    private const string RemovedState = "Removed";

    public sealed record AdvanceJobsRequest : ICommand<Outcome>;

    public sealed record Idle;

    /// <param name="NotBefore">The time before which no ring ends the wait.</param>
    public sealed record WaitUntil(DateTimeOffset At, DateTimeOffset NotBefore);

    public sealed record Stepped;

    public readonly union Outcome(Idle, WaitUntil, Stepped);

    internal sealed partial class Handler(
        JobRegistry jobs,
        CooldownHolder cooldown,
        ActiveStep activeStep,
        ICatalog catalog,
        IAudioSource audio,
        IVideoSource videos,
        ICoverSource covers,
        IReleaseEnricher enricher,
        IJobOutput output,
        Random random,
        TimeProvider timeProvider,
        JobMetrics metrics,
        ILogger<Handler> logger
    ) : IRequestHandler<AdvanceJobsRequest, Outcome>
    {
        public async ValueTask<Outcome> HandleAsync(
            AdvanceJobsRequest request,
            CancellationToken cancellationToken
        )
        {
            var now = timeProvider.GetUtcNow();
            if (cooldown.Current is { Until: { } until } active && active.IsActiveAt(now))
                return new WaitUntil(until, now);

            var job = jobs.Active() ?? jobs.NextQueued();
            if (job is null)
                return new Idle();

            if (job.RetryAt is { } retryAt && retryAt > now)
                return new WaitUntil(retryAt, now);

            // The step exists before the job is read again, so a removal either shows in the read
            // or cancels the step
            using var step = activeStep.Begin(job.Id, cancellationToken);
            if (jobs.Get(job.Id) is not { } registered)
                return new Stepped();

            try
            {
                return await RunAsync(registered, now, step.Token);
            }
            catch (OperationCanceledException)
                when (step.Token.IsCancellationRequested
                    && !cancellationToken.IsCancellationRequested
                )
            {
                return Pause(job: null);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var aborted = await jobs.UpdateAsync(
                    job.Id,
                    current => current.Abort(exception.Message, timeProvider.GetUtcNow()),
                    CancellationToken.None
                );
                if (aborted is not null)
                    throw;

                LogRemovedStepFailed(job.Id.Value, exception);
                return Pause(job: null);
            }
            finally
            {
                // Files can outlive the removal, since the step wrote them after the delete
                if (jobs.Get(job.Id) is null)
                    output.Delete(job.Id);
            }
        }

        private async ValueTask<Outcome> RunAsync(
            Job job,
            DateTimeOffset now,
            CancellationToken cancellationToken
        )
        {
            var current = job.State is JobState.Queued or JobState.Waiting
                ? await jobs.UpdateAsync(job.Id, next => next.Resume(now), cancellationToken)
                : job;
            if (current is null)
                return new Stepped();

            var next = current.NextStep();
            using var run = metrics.StartStep(current.Id, next);
            try
            {
                return next switch
                {
                    JobStep.Resolve step => await ResolveAsync(
                        current,
                        step.Link,
                        run,
                        cancellationToken
                    ),
                    JobStep.DownloadTrack step => await DownloadAsync(
                        current,
                        step.Track,
                        run,
                        cancellationToken
                    ),
                    JobStep.DownloadVideo step => await DownloadVideoAsync(
                        current,
                        step.Video,
                        run,
                        cancellationToken
                    ),
                    JobStep.Package => await PackageAsync(current, cancellationToken),
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                run.Fail(exception.GetType().FullName!);
                throw;
            }
        }

        private async ValueTask<Outcome> ResolveAsync(
            Job job,
            MusicLink link,
            JobMetrics.StepRun run,
            CancellationToken cancellationToken
        )
        {
            var lookup = await catalog.FindAsync(link, cancellationToken);
            var now = timeProvider.GetUtcNow();
            return lookup switch
            {
                Release release when jobs.FindCover(job, release) is { } duplicate => Pause(
                    await jobs.UpdateAsync(
                        job.Id,
                        current => current.Skip(duplicate.Id, now),
                        cancellationToken
                    )
                ),
                Release release => Pause(
                    await jobs.UpdateAsync(
                        job.Id,
                        current => current.Resolve(release),
                        cancellationToken
                    )
                ),
                RemoteFailure failure => await FailAsync(job, failure, run, cancellationToken),
                null => throw Unreachable.NullUnion(),
            };
        }

        private async ValueTask<Outcome> DownloadAsync(
            Job job,
            JobTrack track,
            JobMetrics.StepRun run,
            CancellationToken cancellationToken
        )
        {
            var videoId = track.Track.VideoId;
            if (audio.IsDownloaded(job.Id, videoId))
            {
                await jobs.UpdateAsync(
                    job.Id,
                    current => current.CompleteTrack(),
                    cancellationToken
                );
                return new Stepped();
            }

            var download = await audio.DownloadAsync(
                job.Id,
                videoId,
                fraction => jobs.Update(job.Id, current => current.Progress(fraction)),
                cancellationToken
            );
            return download switch
            {
                Downloaded => Pause(
                    await jobs.UpdateAsync(
                        job.Id,
                        current => current.CompleteTrack(),
                        cancellationToken
                    )
                ),
                RemoteFailure failure => await FailAsync(job, failure, run, cancellationToken),
                null => throw Unreachable.NullUnion(),
            };
        }

        private async ValueTask<Outcome> DownloadVideoAsync(
            Job job,
            JobRequest.Video video,
            JobMetrics.StepRun run,
            CancellationToken cancellationToken
        )
        {
            if (videos.FindKept(job.Id, video.VideoId) is { } kept)
            {
                await jobs.UpdateAsync(
                    job.Id,
                    current => current.CompleteVideo(kept),
                    cancellationToken
                );
                return new Stepped();
            }

            var download = await videos.DownloadAsync(
                job.Id,
                video.VideoId,
                video.Quality,
                fraction => jobs.Update(job.Id, current => current.Progress(fraction)),
                cancellationToken
            );
            return download switch
            {
                VideoInfo info => Pause(
                    await jobs.UpdateAsync(
                        job.Id,
                        current => current.CompleteVideo(info),
                        cancellationToken
                    )
                ),
                RemoteFailure failure => await FailAsync(job, failure, run, cancellationToken),
                null => throw Unreachable.NullUnion(),
            };
        }

        private async ValueTask<Outcome> PackageAsync(Job job, CancellationToken cancellationToken)
        {
            var now = timeProvider.GetUtcNow();
            var packaging = await jobs.UpdateAsync(
                job.Id,
                current => current.StartPackaging(now),
                cancellationToken
            );
            if (packaging is not { State: JobState.Packaging })
                return new Stepped();

            Cover? cover = null;
            ReleaseDetails? details = null;
            EnrichmentFailure? enrichmentFailure = null;
            if (packaging.Release is { } release)
            {
                cover = await covers.FetchAsync(release.CoverUrl, cancellationToken);
                switch (await enricher.EnrichAsync(release, cancellationToken))
                {
                    case ReleaseDetails found:
                        details = found;
                        break;
                    case NotEnriched missed:
                        enrichmentFailure = missed.Failure;
                        LogNotEnriched(job.Id.Value, missed.Failure);
                        break;
                    default:
                        throw Unreachable.NullUnion();
                }

                metrics.RecordEnrichment(enrichmentFailure);
            }

            var file = await output.PackageAsync(packaging, cover, details, cancellationToken);
            var finished = timeProvider.GetUtcNow();
            var done = await jobs.UpdateAsync(
                job.Id,
                current => current.Finish(file.Name, file.Size, enrichmentFailure, finished),
                cancellationToken
            );

            await cooldown.UpdateAsync(current => current.Clear());
            return Pause(done);
        }

        private async ValueTask<Outcome> FailAsync(
            Job job,
            RemoteFailure failure,
            JobMetrics.StepRun run,
            CancellationToken cancellationToken
        )
        {
            run.Fail(YGrabTelemetry.ErrorType(failure));
            var now = timeProvider.GetUtcNow();
            var failed = await jobs.UpdateAsync(
                job.Id,
                current => current.Fail(failure, now),
                cancellationToken
            );
            LogStepFailed(
                job.Id.Value,
                failed?.State.GetType().Name ?? RemovedState,
                failure.Reason
            );

            if (failure is not RemoteFailure.RateLimited)
                return Pause(failed);

            var (hit, _) = await cooldown.UpdateAsync(current => current.Hit(failure.Reason, now));
            LogCooldownStarted(hit.Strikes, hit.Until, failure.Reason);
            return hit.Until is { } until ? new WaitUntil(until, now) : new Stepped();
        }

        private Outcome Pause(Job? job)
        {
            var pause = timeProvider.GetUtcNow() + Pacing.Pause(random.Next(Pacing.PauseChoices));
            return job?.RetryAt is { } retryAt
                ? new WaitUntil(retryAt, pause)
                : new WaitUntil(pause, pause);
        }

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Job {Id} is now {State} after a failed call: {Reason}"
        )]
        private partial void LogStepFailed(Guid id, string state, string reason);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Job {Id} was removed during a step that failed"
        )]
        private partial void LogRemovedStepFailed(Guid id, Exception exception);

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Job {Id} is packaged without provider tags: {Failure}"
        )]
        private partial void LogNotEnriched(Guid id, EnrichmentFailure failure);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "YouTube rate-limited us. Strike {Strikes}, no call until {Until}: {Reason}"
        )]
        private partial void LogCooldownStarted(int strikes, DateTimeOffset? until, string reason);
    }
}
