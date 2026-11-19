// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Host.Auth;

/// <summary>
/// Gives the token store its token before the host serves requests.
/// </summary>
internal sealed class AuthStartup(TokenStore tokens, IOptions<AuthOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Disabled)
            tokens.Initialize();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
