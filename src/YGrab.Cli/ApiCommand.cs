// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.CommandLine;
using YGrab.Cli.Api;

namespace YGrab.Cli;

/// <summary>
/// A command that calls the configured server, and exits with the usage code when none is set.
/// </summary>
internal abstract class ApiCommand : Command
{
    protected ApiCommand(CliSession session, string name, string description)
        : base(name, description)
    {
        Session = session;
        SetAction(
            async (parse, cancellationToken) =>
                session.Api() is { } api
                    ? await RunAsync(api, parse, cancellationToken)
                    : ExitCodes.USAGE
        );
    }

    protected CliSession Session { get; }

    protected Reporter Reporter => Session.Reporter;

    protected abstract Task<int> RunAsync(
        ApiClient api,
        ParseResult parse,
        CancellationToken cancellationToken
    );
}
