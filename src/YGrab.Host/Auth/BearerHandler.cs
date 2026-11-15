// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;

namespace YGrab.Host.Auth;

/// <summary>
/// Signs in a request that carries the token in its Authorization header, for the CLI.
/// </summary>
internal sealed class BearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggers,
    UrlEncoder encoder,
    TokenStore tokens
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggers, encoder)
{
    internal const string SchemeName = "Bearer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (
            !AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header)
            || !string.Equals(header.Scheme, SchemeName, StringComparison.OrdinalIgnoreCase)
        )
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!tokens.Matches(header.Parameter))
        {
            return Task.FromResult(AuthenticateResult.Fail("The token is wrong."));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(SchemeName));
        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName))
        );
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = SchemeName;
        return Task.CompletedTask;
    }
}
