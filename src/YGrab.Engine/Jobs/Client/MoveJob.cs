// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// The registry computes the rank under its lock, so the neighbours cannot change in between.
/// </summary>
public static class MoveJob
{
    public sealed record Command(JobId Id, Destination To);

    public closed record Destination
    {
        public sealed record Before(JobId Job) : Destination;

        public sealed record Last : Destination;
    }

    public sealed record Moved;

    public readonly union Outcome(Moved, NotFound, NotMovable);

    internal sealed class Handler(JobRegistry jobs) : ICommandHandler<Command, Outcome>
    {
        public async ValueTask<Outcome> HandleAsync(
            Command command,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(command);

            var moved = false;
            var updated = await jobs.UpdateAsync(
                command.Id,
                current =>
                {
                    var rank = current.CanMove ? RankOf(current, command.To) : null;
                    moved = rank is not null;
                    return rank is { } value ? current.MoveTo(value) : current;
                },
                cancellationToken
            );
            if (updated is null)
                return new NotFound();

            return moved ? new Moved() : new NotMovable();
        }

        private double? RankOf(Job job, Destination to) =>
            to switch
            {
                Destination.Last => jobs.NextRank(),
                Destination.Before before => JobQueue.RankBefore(job, before.Job, [.. jobs.All()]),
            };
    }
}
