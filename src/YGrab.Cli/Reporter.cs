// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using Spectre.Console;
using YGrab.Cli.Api;

namespace YGrab.Cli;

/// <summary>
/// Writes the results to the standard output and the errors to the standard error.
/// </summary>
internal sealed class Reporter(IAnsiConsole output, IAnsiConsole error)
{
    public IAnsiConsole Output => output;

    public int Report(ApiFailure failure)
    {
        var (message, code) = failure switch
        {
            ApiFailure.Rejected rejected => (Describe(rejected), ExitCodes.API_ERROR),
            ApiFailure.Unresolved unresolved => (unresolved.Message, ExitCodes.API_ERROR),
            ApiFailure.Unreachable unreachable => (
                $"Cannot reach the server: {unreachable.Message}",
                ExitCodes.UNREACHABLE
            ),
            ApiFailure.BadToken => (
                "The server refuses the token. Run ygrab login again, or wait a minute after too many wrong tokens.",
                ExitCodes.UNREACHABLE
            ),
        };
        error.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        return code;
    }

    public int Usage(string message)
    {
        error.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        return ExitCodes.USAGE;
    }

    public int Fail(string message)
    {
        error.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        return ExitCodes.API_ERROR;
    }

    public void Warn(string message) => error.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");

    private static string Describe(ApiFailure.Rejected rejected)
    {
        if (rejected.Problem is not { } problem)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"The server answered {rejected.Status}."
            );
        }

        if (problem.Errors is { Count: > 0 } errors)
            return string.Join(' ', errors.Values.SelectMany(messages => messages));

        return problem.Detail
            ?? problem.Title
            ?? string.Create(
                CultureInfo.InvariantCulture,
                $"The server answered {rejected.Status}."
            );
    }
}
