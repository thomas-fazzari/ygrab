// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.CommandLine;
using System.Text.Json;
using Spectre.Console;
using YGrab.Cli.Auth;
using YGrab.Cli.Jobs;

namespace YGrab.Cli;

internal static class Program
{
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(10);

    public static async Task<int> Main(string[] args)
    {
        using var handler = new SocketsHttpHandler { ConnectTimeout = _connectTimeout };
        using var http = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var error = AnsiConsole.Create(
            new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) }
        );
        var reporter = new Reporter(AnsiConsole.Console, error);
        var session = new CliSession(http, reporter);
        var rootCommand = new RootCommand("Grabs albums, tracks and videos from a YGrab server")
        {
            new LoginCommand(session),
            new AddCommand(session),
            new ListCommand(session),
            new ShowCommand(session),
            new WatchCommand(session),
            new GetCommand(session),
            new RetryCommand(session),
            new RemoveCommand(session),
        };

        var parseResult = rootCommand.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var parseError in parseResult.Errors)
            {
                reporter.Usage(parseError.Message);
            }

            return ExitCodes.Usage;
        }

        try
        {
            return await parseResult.InvokeAsync(
                new InvocationConfiguration { EnableDefaultExceptionHandler = false },
                CancellationToken.None
            );
        }
        catch (OperationCanceledException)
        {
            return ExitCodes.Interrupted;
        }
        catch (JsonException exception)
        {
            return reporter.Fail(
                $"The server sent data that the CLI cannot read: {exception.Message}"
            );
        }
    }
}
