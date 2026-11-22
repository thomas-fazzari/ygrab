// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine.Jobs;

/// <summary>
/// Ends the pause of the cooldown now and resumes the jobs.
/// </summary>
public static class EndCooldown
{
    public sealed record Command;

    internal sealed class Handler(
        CooldownHolder cooldown,
        JobSignal signal,
        TimeProvider timeProvider
    ) : ICommandHandler<Command, Done>
    {
        public async ValueTask<Done> HandleAsync(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var now = timeProvider.GetUtcNow();
            var (_, ended) = await cooldown.UpdateAsync(current =>
                current.IsActiveAt(now) ? current.End() : current
            );
            if (ended)
                signal.Ring();

            return default;
        }
    }
}
