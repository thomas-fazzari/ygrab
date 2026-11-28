// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;

namespace YGrab.Host.Auth;

internal sealed class BearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggers,
    UrlEncoder encoder,
    TokenStore tokens,
    WrongTokenLimiter limiter
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggers, encoder)
{
    internal const string SCHEME_NAME = "Bearer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (HeaderOf(Request) is not { } header)
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!limiter.CanTry(Context))
            return Task.FromResult(AuthenticateResult.Fail("Too many wrong tokens."));

        if (!tokens.Matches(header.Parameter))
        {
            limiter.CountWrong(Context);
            return Task.FromResult(AuthenticateResult.Fail("The token is wrong."));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(SCHEME_NAME));
        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(principal, SCHEME_NAME))
        );
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = SCHEME_NAME;
        return Task.CompletedTask;
    }

    internal static AuthenticationHeaderValue? HeaderOf(HttpRequest request) =>
        AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var header)
        && string.Equals(header.Scheme, SCHEME_NAME, StringComparison.OrdinalIgnoreCase)
            ? header
            : null;
}
