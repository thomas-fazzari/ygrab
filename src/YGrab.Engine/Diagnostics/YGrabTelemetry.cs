// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Diagnostics;

/// <summary>
/// The name of the spans and the metrics of YGrab, and the source of its spans.
/// </summary>
public static class YGrabTelemetry
{
    public const string Name = "YGrab";

    /// <summary>
    /// The <c>error.type</c> tag of the OpenTelemetry semantic conventions.
    /// </summary>
    public const string ErrorTypeTag = "error.type";

    public static ActivitySource Source { get; } = new(Name);

    public static void MarkFailed(Activity? activity, string errorType)
    {
        activity?.SetTag(ErrorTypeTag, errorType);
        activity?.SetStatus(ActivityStatusCode.Error);
    }

    public static string ErrorType(RemoteFailure failure) =>
        failure switch
        {
            RemoteFailure.RateLimited => "rate_limited",
            RemoteFailure.Transient => "transient",
            RemoteFailure.Unavailable => "unavailable",
        };
}
