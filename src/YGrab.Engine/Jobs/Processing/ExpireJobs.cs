// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Logging;
using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Expires the finished jobs that are older than their retention and deletes their files.
/// </summary>
/// <remarks>
/// <para>
/// A video job has its own retention.
/// </para>
/// <para>
/// A ready or incomplete job becomes expired and stays in the history, without its file.
/// </para>
/// <para>
/// A failed job keeps its state and loses its folder.
/// </para>
/// <para>
/// A job that cannot be swept is logged and the sweep goes on with the others.
/// </para>
/// <para>
/// The sweep reads the job again before it deletes the folder, since a retry can have queued it.
/// </para>
/// </remarks>
public static partial class ExpireJobs
{
    /// <param name="Retention">How long a finished audio job keeps its folder.</param>
    /// <param name="VideoRetention">How long a finished video job keeps its folder.</param>
    public sealed record Command(TimeSpan Retention, TimeSpan VideoRetention);

    internal sealed partial class Handler(
        JobRegistry jobs,
        IJobOutput output,
        TimeProvider timeProvider,
        ILogger<Handler> logger
    ) : ICommandHandler<Command, Done>
    {
        public async ValueTask<Done> HandleAsync(
            Command command,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(command);

            var now = timeProvider.GetUtcNow();
            foreach (var job in jobs.List(int.MaxValue))
            {
                if (!IsDue(job, command, now))
                {
                    continue;
                }

                try
                {
                    await ExpireAsync(job.Id, command, now, cancellationToken);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    LogExpireFailed(job.Id, exception);
                }
            }

            return new Done();
        }

        private static bool IsDue(Job job, Command command, DateTimeOffset now)
        {
            var retention =
                job.Request is JobRequest.Video ? command.VideoRetention : command.Retention;
            return job.State is JobState.Ready or JobState.Incomplete or JobState.Failed
                && job.FinishedAt < now - retention;
        }

        // The folder goes first, so a failed delete leaves the job to the next sweep
        private async ValueTask ExpireAsync(
            Guid id,
            Command command,
            DateTimeOffset now,
            CancellationToken cancellationToken
        )
        {
            if (jobs.Get(id) is not { } job || !IsDue(job, command, now))
            {
                return;
            }

            output.Delete(id);
            if (job.State is not JobState.Failed)
            {
                await jobs.UpdateAsync(id, current => current.Expire(), cancellationToken);
            }
        }

        [LoggerMessage(Level = LogLevel.Error, Message = "Expiring the job {Id} failed")]
        private partial void LogExpireFailed(Guid id, Exception exception);
    }
}
