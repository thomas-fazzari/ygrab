// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using YGrab.Host.Extensions;

namespace YGrab.Host;

// A malformed request keeps the status of its exception, such as 413 for a body over the limit
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    IHostEnvironment environment
) : IExceptionHandler
{
    internal const string BadRequestType = FailureExtensions.TypePrefix + "bad-request";

    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    ) =>
        exception is BadHttpRequestException badRequest
            ? WriteAsync(
                httpContext,
                exception,
                new ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Type = BadRequestType,
                    Title = "Bad request",
                }
            )
            : WriteAsync(
                httpContext,
                exception,
                new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Type = FailureExtensions.TypePrefix + "unexpected",
                    Title = "Unexpected error",
                    Detail = environment.IsDevelopment() ? exception.ToString() : null,
                }
            );

    private ValueTask<bool> WriteAsync(
        HttpContext httpContext,
        Exception exception,
        ProblemDetails problem
    )
    {
        httpContext.Response.StatusCode = problem.Status
            ?? StatusCodes.Status500InternalServerError;
        return problemDetails.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = problem,
            }
        );
    }
}
