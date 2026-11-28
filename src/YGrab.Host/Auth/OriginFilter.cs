// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Host.Auth;

/// <summary>
/// Refuses an unsafe request from another site, unless it carries a bearer token.
/// </summary>
/// <remarks>
/// <para>
/// SameSite=Strict still lets a sibling subdomain send the cookie, so the request needs
/// <c>Sec-Fetch-Site: same-origin</c> or an <c>Origin</c> that matches its own host.
/// </para>
/// <para>
/// A browser never adds a bearer token on its own, and another site cannot set one without a CORS
/// preflight that the host does not answer. Any other request needs the same origin, even an
/// anonymous one such as the login.
/// </para>
/// </remarks>
internal sealed class OriginFilter : IEndpointFilter
{
    internal const string SEC_FETCH_SITE_HEADER = "Sec-Fetch-Site";

    private const string SAME_ORIGIN = "same-origin";

    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var request = context.HttpContext.Request;
        return IsSafeMethod(request.Method) || HasBearerToken(request) || IsSameOrigin(request)
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

    private static bool HasBearerToken(HttpRequest request) =>
        BearerHandler.HeaderOf(request) is not null;

    private static bool IsSameOrigin(HttpRequest request)
    {
        if (
            string.Equals(
                request.Headers[SEC_FETCH_SITE_HEADER],
                SAME_ORIGIN,
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
