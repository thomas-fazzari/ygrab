// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.CommandLine;
using Spectre.Console;
using YGrab.Cli.Api;
using YGrab.Contracts.Jobs;

namespace YGrab.Cli.Jobs;

internal sealed class ShowCommand(CliSession session)
    : JobCommand(session, "show", "Shows a job and its tracks")
{
    protected override Task<int> RunAsync(
        ApiClient api,
        JobDto job,
        ParseResult parse,
        CancellationToken cancellationToken
    )
    {
        var output = Reporter.Output;
        output.WriteLine($"{JobIds.Short(job.Id)} {JobView.Label(job)}");
        output.MarkupLine($"State: {JobView.StateMarkup(job.State)}");
        if (job.Reason is not null)
        {
            output.WriteLine($"Reason: {job.Reason}");
        }

        if (job.Artists.Length > 0)
        {
            output.WriteLine($"Artists: {string.Join(", ", job.Artists)}");
        }

        output.WriteLine($"Link: {job.Url}");
        if (job.EnrichmentFailure is { } enrichment)
        {
            output.WriteLine($"Provider tags: missing ({WireNames.Of(enrichment)})");
        }

        if (job.Tracks.Length > 0)
        {
            output.Write(TracksTable(job));
        }

        return Task.FromResult(ExitCodes.Ok);
    }

    private static Table TracksTable(JobDto job)
    {
        var table = new Table().AddColumns("#", "Title", "State");
        foreach (var track in job.Tracks)
        {
            var state = WireNames.Of(track.State);
            if (track.Reason is not null)
            {
                state = $"{state} ({track.Reason})";
            }

            table.AddRow(
                track.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Markup.Escape(track.Title),
                Markup.Escape(state)
            );
        }

        return table;
    }
}
