// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Loads the stored jobs and the cooldown at start.
/// </summary>
/// <remarks>
/// A job that a stopped process interrupted goes back to the queue with its tracks.
/// </remarks>
public static class RestoreJobs
{
    public sealed record Command;

    internal sealed class Handler(
        JobRegistry jobs,
        CooldownHolder cooldown,
        IJobStore store,
        JobSignal signal
    ) : ICommandHandler<Command, Done>
    {
        public async ValueTask<Done> HandleAsync(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var snapshot = await store.LoadAsync(cancellationToken);

            var restored = ImmutableArray.CreateBuilder<Job>(snapshot.Jobs.Length);
            foreach (var job in snapshot.Jobs)
            {
                var requeued = job.Requeue();
                if (requeued != job)
                    await store.SaveAsync(() => requeued, cancellationToken);

                restored.Add(requeued);
            }

            jobs.Load(restored.MoveToImmutable());
            cooldown.Set(snapshot.Cooldown);
            signal.Ring();
            return new Done();
        }
    }
}
