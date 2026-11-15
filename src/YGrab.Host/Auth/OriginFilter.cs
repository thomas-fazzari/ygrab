// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace YGrab.Host.Auth;

/// <summary>
/// Refuses an unsafe request that a session cookie signed in, unless the request is same-origin.
/// </summary>
/// <remarks>
/// <para>
/// SameSite=Strict still lets a sibling subdomain send the cookie, so the request needs
/// <c>Sec-Fetch-Site: same-origin</c> or an <c>Origin</c> that matches its own host.
/// </para>
/// <para>
/// A request with a bearer token or without auth has no cookie to abuse and passes.
/// </para>
/// </remarks>
internal sealed class OriginFilter : IEndpointFilter
{
    internal const string SecFetchSiteHeader = "Sec-Fetch-Site";

    private const string SameOrigin = "same-origin";

    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;
        if (IsSafeMethod(http.Request.Method) || !IsCookieOnly(http.User))
        {
            return next(context);
        }

        return IsSameOrigin(http.Request)
            ? next(context)
            : ValueTask.FromResult<object?>(
                TypedResults.StatusCode(StatusCodes.Status403Forbidden)
            );
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method)
        || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method)
        || HttpMethods.IsTrace(method);

    private static bool IsCookieOnly(ClaimsPrincipal user)
    {
        var signedIn = user.Identities.Where(identity => identity.IsAuthenticated).ToList();
        return signedIn.Count > 0
            && signedIn.All(identity =>
                string.Equals(
                    identity.AuthenticationType,
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    StringComparison.Ordinal
                )
            );
    }

    private static bool IsSameOrigin(HttpRequest request)
    {
        if (
            string.Equals(
                request.Headers[SecFetchSiteHeader],
                SameOrigin,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return true;
        }

        var origin = request.Headers.Origin.ToString();
        return !string.IsNullOrWhiteSpace(origin)
            && string.Equals(
                origin,
                $"{request.Scheme}://{request.Host}",
                StringComparison.OrdinalIgnoreCase
            );
    }
}
