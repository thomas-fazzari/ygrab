// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace YGrab.Host.Auth;

internal static class SessionCookie
{
    internal const string NAME = "ygrab";
    internal const string TOKEN_HASH_CLAIM = "token-hash";
    internal static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static ClaimsPrincipal CreatePrincipal(byte[] tokenHash) =>
        new(
            new ClaimsIdentity(
                [new Claim(TOKEN_HASH_CLAIM, Convert.ToBase64String(tokenHash))],
                CookieAuthenticationDefaults.AuthenticationScheme
            )
        );

    public static Task SignInAsync(HttpContext context, byte[] tokenHash) =>
        context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CreatePrincipal(tokenHash),
            new AuthenticationProperties { IsPersistent = true }
        );

    public static Task SignOutAsync(HttpContext context) =>
        context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    // A new token signs every browser out
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var tokens = context.HttpContext.RequestServices.GetRequiredService<TokenStore>();
        if (
            context.Principal?.FindFirstValue(TOKEN_HASH_CLAIM) is { } hash
            && tokens.MatchesHash(hash)
        )
        {
            return;
        }

        context.RejectPrincipal();
        await SignOutAsync(context.HttpContext);
    }
}
