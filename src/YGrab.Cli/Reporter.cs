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
            ApiFailure.Rejected rejected => (Describe(rejected), ExitCodes.ApiError),
            ApiFailure.Unresolved unresolved => (unresolved.Message, ExitCodes.ApiError),
            ApiFailure.Unreachable unreachable => (
                $"Cannot reach the server: {unreachable.Message}",
                ExitCodes.Unreachable
            ),
            ApiFailure.BadToken => (
                "The server refuses the token. Run ygrab login again.",
                ExitCodes.Unreachable
            ),
        };
        error.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        return code;
    }

    public int Usage(string message)
    {
        error.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        return ExitCodes.Usage;
    }

    public int Fail(string message)
    {
        error.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        return ExitCodes.ApiError;
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
        {
            return string.Join(' ', errors.Values.SelectMany(messages => messages));
        }

        return problem.Detail
            ?? problem.Title
            ?? string.Create(
                CultureInfo.InvariantCulture,
                $"The server answered {rejected.Status}."
            );
    }
}
