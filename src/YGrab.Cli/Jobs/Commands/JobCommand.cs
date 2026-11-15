// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.CommandLine;
using YGrab.Cli.Api;
using YGrab.Contracts.Jobs;

namespace YGrab.Cli.Jobs;

/// <summary>
/// A command on one job, which it finds from the id argument.
/// </summary>
internal abstract class JobCommand : ApiCommand
{
    private readonly Argument<string> _id = new("id")
    {
        Description = "The job id, or its last characters",
    };

    protected JobCommand(CliSession session, string name, string description)
        : base(session, name, description) => Arguments.Add(_id);

    protected sealed override async Task<int> RunAsync(
        ApiClient api,
        ParseResult parse,
        CancellationToken cancellationToken
    )
    {
        var found = await JobIds.FindAsync(api, parse.GetRequiredValue(_id), cancellationToken);
        return found.TryGetValue(out var job, out var failure)
            ? await RunAsync(api, job, parse, cancellationToken)
            : Reporter.Report(failure);
    }

    protected abstract Task<int> RunAsync(
        ApiClient api,
        JobDto job,
        ParseResult parse,
        CancellationToken cancellationToken
    );
}
