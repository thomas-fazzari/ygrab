// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.CommandLine;
using System.Globalization;
using Spectre.Console;
using YGrab.Cli.Api;

namespace YGrab.Cli.Jobs;

internal sealed class ListCommand(CliSession session)
    : ApiCommand(session, "ls", "Lists the jobs, newest first")
{
    protected override async Task<int> RunAsync(
        ApiClient api,
        ParseResult parse,
        CancellationToken cancellationToken
    )
    {
        var listed = await api.ListJobsAsync(limit: null, cancellationToken);
        if (!listed.TryGetValue(out var jobs, out var failure))
            return Reporter.Report(failure);

        var table = new Table().AddColumns("Id", "State", "Progress", "Job");
        foreach (var job in jobs)
        {
            table.AddRow(
                JobIds.Short(job.Id),
                JobView.StateMarkup(job.State),
                string.Create(CultureInfo.InvariantCulture, $"{job.Percent}%"),
                Markup.Escape(JobView.Label(job))
            );
        }

        Reporter.Output.Write(table);
        return ExitCodes.OK;
    }
}
