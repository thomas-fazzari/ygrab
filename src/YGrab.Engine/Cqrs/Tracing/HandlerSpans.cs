// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using YGrab.Engine.Diagnostics;

namespace YGrab.Engine;

/// <summary>
/// Runs a handler in a span named after its use case, tagged with the case of its outcome.
/// </summary>
internal static class HandlerSpans
{
    private const string OutcomeTag = "ygrab.outcome";

    public static async ValueTask<T> TraceAsync<T>(Type message, Func<ValueTask<T>> handle)
    {
        // StartJob.Command gives the span StartJob
        using var activity = YGrabTelemetry.Source.StartActivity(
            message.DeclaringType?.Name ?? message.Name
        );
        try
        {
            var outcome = await handle();
            Record(activity, outcome);
            return outcome;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            YGrabTelemetry.MarkFailed(activity, exception.GetType().FullName ?? "exception");
            throw;
        }
    }

    private static void Record(Activity? activity, object? outcome)
    {
        if (activity is null)
            return;

        var value = outcome is IUnion union ? union.Value : outcome;
        activity.SetTag(OutcomeTag, value?.GetType().Name);
        if (value is Failure)
            YGrabTelemetry.MarkFailed(activity, value.GetType().Name);
    }
}
