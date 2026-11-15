// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

public static class GetCooldown
{
    public sealed record Query;

    internal sealed class Handler(CooldownHolder cooldown) : IQueryHandler<Query, Cooldown>
    {
        public ValueTask<Cooldown> HandleAsync(Query query, CancellationToken cancellationToken) =>
            ValueTask.FromResult(cooldown.Current);
    }
}
