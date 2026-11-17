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
/// A ready or incomplete job becomes expired and stays in the history, without its file.
/// </para>
/// <para>
/// A job that finished longer ago than <see cref="Retention.History"/> is removed with its row
/// and its files, through <see cref="RemoveJob"/>.
/// </para>
/// <para>
/// A failed job keeps its state and loses its folder.
/// </para>
/// <para>
/// A job that cannot be swept is logged and the sweep goes on with the others.
/// </para>
/// <para>
/// The sweep reads the job again before it deletes the folder or removes the job, since a retry
/// can have queued it.
/// </para>
/// </remarks>
public static partial class ExpireJobs
{
    public sealed record Command(Retention Retention);

    internal sealed partial class Handler(
        JobRegistry jobs,
        IJobOutput output,
        ICommandHandler<RemoveJob.Command, RemoveJob.Outcome> removeJob,
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
                try
                {
                    await SweepAsync(job, command, now, cancellationToken);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    LogSweepFailed(job.Id, exception);
                }
            }

            return new Done();
        }

        private static bool IsDue(Job job, Command command, DateTimeOffset now) =>
            job.State is not JobState.Expired && command.Retention.ExpiresAt(job) < now;

        private static bool IsForgotten(Job job, Command command, DateTimeOffset now) =>
            command.Retention.RemovedAt(job) < now;

        private async ValueTask SweepAsync(
            Job job,
            Command command,
            DateTimeOffset now,
            CancellationToken cancellationToken
        )
        {
            if (IsForgotten(job, command, now))
            {
                await ForgetAsync(job.Id, command, now, cancellationToken);
                return;
            }

            if (IsDue(job, command, now))
            {
                await ExpireAsync(job.Id, command, now, cancellationToken);
            }
        }

        private async ValueTask ForgetAsync(
            Guid id,
            Command command,
            DateTimeOffset now,
            CancellationToken cancellationToken
        )
        {
            if (jobs.Get(id) is { } job && IsForgotten(job, command, now))
            {
                await removeJob.HandleAsync(new RemoveJob.Command(id), cancellationToken);
            }
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

        [LoggerMessage(Level = LogLevel.Error, Message = "Sweeping the job {Id} failed")]
        private partial void LogSweepFailed(Guid id, Exception exception);
    }
}
