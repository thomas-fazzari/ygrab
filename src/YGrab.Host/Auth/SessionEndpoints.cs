// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.RateLimiting;
using YGrab.Contracts.Auth;

namespace YGrab.Host.Auth;

internal static class SessionEndpoints
{
    internal const string LoginPolicy = "login";
    internal const int LoginAttempts = 5;
    internal static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(1);

    extension(IServiceCollection services)
    {
        public IServiceCollection AddLoginRateLimit() =>
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy(LoginPolicy, context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = LoginAttempts,
                            Window = LoginWindow,
                            QueueLimit = 0,
                        }
                    )
                );
            });
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapSession()
        {
            endpoints
                .MapPost("/login", LoginAsync)
                .AllowAnonymous()
                .RequireRateLimiting(LoginPolicy);
            endpoints.MapPost("/logout", (Delegate)LogoutAsync).AllowAnonymous();
            endpoints.MapGet("/session", GetSession);
            return endpoints;
        }
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult>> LoginAsync(
        LoginRequest request,
        TokenStore tokens,
        HttpContext context
    )
    {
        if (!tokens.Matches(request.Token) || tokens.CurrentHash() is not { } hash)
        {
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
