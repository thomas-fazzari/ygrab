// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.CommandLine;
using Spectre.Console;
using YGrab.Cli.Api;
using YGrab.Contracts.Jobs;

namespace YGrab.Cli.Jobs;

internal sealed class RetryCommand(CliSession session)
    : JobCommand(session, "retry", "Queues a failed or incomplete job again")
{
    protected override async Task<int> RunAsync(
        ApiClient api,
        JobDto job,
        ParseResult parse,
        CancellationToken cancellationToken
    )
    {
        var retried = await api.RetryJobAsync(job.Id, cancellationToken);
        if (!retried.TryGetValue(out _, out var failure))
        {
            return Reporter.Report(failure);
        }

        Reporter.Output.WriteLine($"{JobIds.Short(job.Id)} queued again");
        return ExitCodes.Ok;
    }
}
