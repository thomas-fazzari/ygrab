// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

public static class GetCooldown
{
    public sealed record GetCooldownRequest : IQuery<Cooldown>;

    internal sealed class Handler(CooldownHolder cooldown)
        : IRequestHandler<GetCooldownRequest, Cooldown>
    {
        public ValueTask<Cooldown> HandleAsync(
            GetCooldownRequest request,
            CancellationToken cancellationToken
        ) => ValueTask.FromResult(cooldown.Current);
    }
}
