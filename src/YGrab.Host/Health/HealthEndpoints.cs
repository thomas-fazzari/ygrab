// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net.Mime;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace YGrab.Host.Health;

internal static class HealthEndpoints
{
    private const string Banner = """
        _____.___. ________            ___.
        \__  |   |/  _____/___________ \_ |__
         /   |   /   \  __\_  __ \__  \ | __ \
         \____   \    \_\  \  | \// __ \| \_\ \
         / ______|\______  /__|  (____  /___  /
         \/              \/           \/    \/

                 Healthy, and running.

        """;

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapHealth()
        {
            endpoints
                .MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteAsync })
                .AllowAnonymous();
            return endpoints;
        }
    }

    private static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = MediaTypeNames.Text.Plain;
        var body = report.Status == HealthStatus.Healthy ? Banner : report.Status.ToString();
        return context.Response.WriteAsync(body, context.RequestAborted);
    }
}
