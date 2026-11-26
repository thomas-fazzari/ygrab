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
    public sealed record RestoreJobsRequest : ICommand<Done>;

    internal sealed class Handler(
        JobRegistry jobs,
        CooldownHolder cooldown,
        IJobStore store,
        JobSignal signal
    ) : IRequestHandler<RestoreJobsRequest, Done>
    {
        public async ValueTask<Done> HandleAsync(
            RestoreJobsRequest request,
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
            cooldown.Load(snapshot.Cooldown);
            signal.Ring();
            return new Done();
        }
    }
}
