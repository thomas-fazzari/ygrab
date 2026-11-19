// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using Spectre.Console;
using YGrab.Cli.Api;
using YGrab.Contracts.Jobs;

namespace YGrab.Cli.Jobs;

internal static class JobView
{
    public static string Label(JobDto job) => job.Title ?? job.Url.ToString();

    public static string StateMarkup(JobStateCode state) =>
        $"[{StateColor(state)}]{WireNames.Of(state)}[/]";

    public static string Progress(JobDto job)
    {
        var parts = new List<string> { JobIds.Short(job.Id), Markup.Escape(Label(job)) };
        if (job.Tracks.Length > 0)
        {
            var done = job.Tracks.Count(track => track.State is not TrackStateCode.Pending);
            parts.Add(
                string.Create(CultureInfo.InvariantCulture, $"[[{done}/{job.Tracks.Length}]]")
            );
        }

        if (job.Tracks.FirstOrDefault(track => track.Number == job.CurrentTrack) is { } current)
            parts.Add(Markup.Escape(current.Title));

        parts.Add($"({WireNames.Of(job.State)})");
        return string.Join(' ', parts);
    }

    /// <summary>
    /// Says how a finished job ended.
    /// </summary>
    /// <returns>The exit code of the job.</returns>
    public static int Conclude(Reporter reporter, JobDto job)
    {
        var id = JobIds.Short(job.Id);
        switch (job.State)
        {
            case JobStateCode.Ready:
                return ExitCodes.Ok;
            case JobStateCode.Incomplete:
                reporter.Warn($"{id} is incomplete: some tracks are missing.");
                return ExitCodes.Ok;
            default:
                var reason = job.Reason is null ? string.Empty : $": {job.Reason}";
                var code = reporter.Fail($"{id} {WireNames.Of(job.State)}{reason}");
                if (job.CanFallBackToVideo)
                {
                    reporter.Warn(
                        $"Run ygrab add --video --quality {WireNames.Of(VideoQualityCode.Audio)} '{job.Url}' to keep its sound only."
                    );
                }

                return code;
        }
    }

    private static string StateColor(JobStateCode state) =>
        state switch
        {
            JobStateCode.Ready => "green",
            JobStateCode.Incomplete => "yellow",
            JobStateCode.Waiting => "yellow",
            JobStateCode.Failed => "red",
            JobStateCode.Skipped => "grey",
            JobStateCode.Expired => "grey",
            JobStateCode.Queued => "blue",
            JobStateCode.Resolving => "blue",
            JobStateCode.Downloading => "blue",
            JobStateCode.Packaging => "blue",
            _ => throw new InvalidOperationException("A job state has no color."),
        };
}
