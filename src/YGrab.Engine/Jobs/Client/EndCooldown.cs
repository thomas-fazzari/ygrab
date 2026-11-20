// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine.Jobs;

/// <summary>
/// Ends the pause of the cooldown now and resumes the jobs.
/// </summary>
/// <remarks>
/// No step runs during the pause, so no rate limit changes the cooldown while it ends.
/// </remarks>
public static class EndCooldown
{
    public sealed record Command;

    internal sealed class Handler(
        CooldownHolder cooldown,
        IJobStore store,
        JobSignal signal,
        TimeProvider timeProvider
    ) : ICommandHandler<Command, Done>
    {
        public async ValueTask<Done> HandleAsync(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var current = cooldown.Current;
            if (!current.IsActiveAt(timeProvider.GetUtcNow()))
                return default;

            var ended = current.End();
            cooldown.Set(ended);
            await store.SaveAsync(ended, CancellationToken.None);
            signal.Ring();
            return default;
        }
    }
}
