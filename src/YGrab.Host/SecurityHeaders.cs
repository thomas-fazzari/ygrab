// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Host;

internal static class SecurityHeaders
{
    private const string ContentSecurityPolicy =
        "default-src 'none'; script-src 'self'; style-src 'self'; font-src 'self'; "
        + "connect-src 'self'; manifest-src 'self'; "
        + "img-src 'self' https://*.googleusercontent.com https://*.ggpht.com https://*.ytimg.com; "
        + "base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    private const string PermissionsPolicy =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), "
        + "microphone=(), payment=(), usb=()";

    private const string NoStore = "no-store";

    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseSecurityHeaders() =>
            app.Use(
                (context, next) =>
                {
                    context.Response.OnStarting(AddAsync, context);
                    return next(context);
                }
            );
    }

    private static Task AddAsync(object state)
    {
        var context = (HttpContext)state;
        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = PermissionsPolicy;
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        if (
            context.Request.Path.StartsWithSegments(
                EndpointExtensions.ApiPrefix,
                StringComparison.Ordinal
            ) && string.IsNullOrWhiteSpace(headers.CacheControl)
        )
        {
            headers.CacheControl = NoStore;
        }

        return Task.CompletedTask;
    }
}
