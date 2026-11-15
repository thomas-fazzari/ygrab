// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.CommandLine;
using Spectre.Console;
using YGrab.Cli.Api;
using YGrab.Contracts.Jobs;

namespace YGrab.Cli.Jobs;

internal sealed class RemoveCommand(CliSession session)
    : JobCommand(session, "rm", "Cancels a job and deletes it with its files")
{
    protected override async Task<int> RunAsync(
        ApiClient api,
        JobDto job,
        ParseResult parse,
        CancellationToken cancellationToken
    )
    {
        var removed = await api.RemoveJobAsync(job.Id, cancellationToken);
        if (!removed.TryGetValue(out _, out var failure))
        {
            return Reporter.Report(failure);
        }

        Reporter.Output.WriteLine($"{JobIds.Short(job.Id)} removed");
        return ExitCodes.Ok;
    }
}
