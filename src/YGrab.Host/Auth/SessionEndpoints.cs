// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Contracts.Auth;

namespace YGrab.Host.Auth;

internal static class SessionEndpoints
{
    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapSession()
        {
            endpoints.MapPost("/login", LoginAsync).AllowAnonymous();
            endpoints.MapPost("/logout", (Delegate)LogoutAsync).AllowAnonymous();
            endpoints.MapGet("/session", GetSession);
            return endpoints;
        }
    }

    private static async Task<
        Results<NoContent, UnauthorizedHttpResult, StatusCodeHttpResult>
    > LoginAsync(
        LoginRequest request,
        TokenStore tokens,
        WrongTokenLimiter limiter,
        HttpContext context
    )
    {
        if (!limiter.CanTry(context))
        {
            return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        if (!tokens.Matches(request.Token) || tokens.CurrentHash() is not { } hash)
        {
            limiter.CountWrong(context);
            return TypedResults.Unauthorized();
        }

        await SessionCookie.SignInAsync(context, hash);
        return TypedResults.NoContent();
    }

    private static Ok<SessionDto> GetSession(IOptions<AuthOptions> options) =>
        TypedResults.Ok(new SessionDto(AuthEnabled: !options.Value.Disabled));

    private static async Task<NoContent> LogoutAsync(HttpContext context)
    {
        await SessionCookie.SignOutAsync(context);
        return TypedResults.NoContent();
    }
}
