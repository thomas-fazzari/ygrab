// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.CommandLine;
using YGrab.Cli.Api;
using YGrab.Contracts.Jobs;

namespace YGrab.Cli.Jobs;

internal sealed class GetCommand : JobCommand
{
    private readonly Option<string?> _output = new("--output", "-o")
    {
        Description = "The file to write, or a folder. The default is the current folder",
    };

    public GetCommand(CliSession session)
        : base(session, "get", "Downloads the file of a finished job") => Options.Add(_output);

    protected override Task<int> RunAsync(
        ApiClient api,
        JobDto job,
        ParseResult parse,
        CancellationToken cancellationToken
    ) => FileDownloader.SaveAsync(Reporter, api, job, parse.GetValue(_output), cancellationToken);
}
