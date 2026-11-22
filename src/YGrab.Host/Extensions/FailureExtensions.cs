// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.AspNetCore.Mvc;
using YGrab.Engine;
using EngineNotFound = YGrab.Engine.NotFound;

namespace YGrab.Host.Extensions;

/// <summary>
/// Maps a shared failure outcome to its ProblemDetails. The type is a relative URI.
/// </summary>
internal static class FailureExtensions
{
    internal const string PreviousIdMember = "previousId";

    private const string TypePrefix = "/problems/";

    extension(Failure failure)
    {
        public ProblemHttpResult ToProblem() =>
            failure switch
            {
                Invalid invalid => ValidationProblem(invalid),
                EngineNotFound => Problem(StatusCodes.Status404NotFound, "not-found", "Not found"),
                NotReady => Problem(StatusCodes.Status409Conflict, "not-ready", "Not ready"),
                Expired => Problem(StatusCodes.Status410Gone, "expired", "Expired"),
                NotRetryable => Problem(
                    StatusCodes.Status409Conflict,
                    "not-retryable",
                    "Not retryable"
                ),
                NotMovable => Problem(StatusCodes.Status409Conflict, "not-movable", "Not movable"),
                AlreadyGrabbed grabbed => AlreadyGrabbedProblem(grabbed),
            };
    }

    private static ProblemHttpResult ValidationProblem(Invalid invalid) =>
        TypedResults.Problem(
            new HttpValidationProblemDetails(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [JsonNamingPolicy.CamelCase.ConvertName(invalid.Field)] = [invalid.Message],
                }
            )
            {
                Status = StatusCodes.Status400BadRequest,
                Type = TypePrefix + "validation",
                Title = "Validation failed",
            }
        );

    private static ProblemHttpResult AlreadyGrabbedProblem(AlreadyGrabbed grabbed) =>
        TypedResults.Problem(
            new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Type = TypePrefix + "already-grabbed",
                Title = "Already grabbed",
                Extensions = { [PreviousIdMember] = grabbed.Previous.Id.Value },
            }
        );

    private static ProblemHttpResult Problem(int statusCode, string type, string title) =>
        TypedResults.Problem(statusCode: statusCode, title: title, type: TypePrefix + type);
}
