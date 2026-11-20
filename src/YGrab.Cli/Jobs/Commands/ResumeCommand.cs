// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.CommandLine;
using Spectre.Console;
using YGrab.Cli.Api;

namespace YGrab.Cli.Jobs;

internal sealed class ResumeCommand(CliSession session)
    : ApiCommand(
        session,
        "resume",
        "Ends the pause after a YouTube rate limit and resumes the jobs"
    )
{
    protected override async Task<int> RunAsync(
        ApiClient api,
        ParseResult parse,
        CancellationToken cancellationToken
    )
    {
        var ended = await api.EndCooldownAsync(cancellationToken);
        if (!ended.TryGetValue(out _, out var failure))
            return Reporter.Report(failure);

        Reporter.Output.WriteLine("Jobs resumed");
        return ExitCodes.Ok;
    }
}
