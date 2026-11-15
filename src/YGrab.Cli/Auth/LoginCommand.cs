// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.CommandLine;
using System.CommandLine.Parsing;
using Spectre.Console;
using YGrab.Cli.Api;
using YGrab.Cli.Configuration;

namespace YGrab.Cli.Auth;

internal sealed class LoginCommand : Command
{
    private const int CheckLimit = 1;

    private readonly CliSession _session;

    private readonly Argument<Uri> _url = new("url")
    {
        Description = "The address of the server, such as https://ygrab.example.com",
        CustomParser = ParseUrl,
    };

    public LoginCommand(CliSession session)
        : base("login", "Checks a token against a server and saves both")
    {
        _session = session;
        Arguments.Add(_url);
        SetAction(RunAsync);
    }

    private async Task<int> RunAsync(ParseResult parse, CancellationToken cancellationToken)
    {
        var url = parse.GetRequiredValue(_url);
        var token = await ReadTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return _session.Reporter.Usage("The token is empty.");
        }

        var checkedToken = await new ApiClient(_session.Http, url, token).ListJobsAsync(
            CheckLimit,
            cancellationToken
        );
        if (!checkedToken.TryGetValue(out _, out var failure))
        {
            return _session.Reporter.Report(failure);
        }

        CliConfigStore.Save(new CliConfig(url.AbsoluteUri, token));
        _session.Reporter.Output.WriteLine(
            $"Logged in to {url}. Saved in {CliConfigStore.FilePath}"
        );
        return ExitCodes.Ok;
    }

    private static Uri? ParseUrl(ArgumentResult result)
    {
        if (CliSession.TryParseUrl(result.Tokens[0].Value, out var url))
        {
            return url;
        }

        result.AddError("The url is not an absolute http or https address.");
        return null;
    }

    private async Task<string?> ReadTokenAsync(CancellationToken cancellationToken) =>
        Console.IsInputRedirected
            ? (await Console.In.ReadLineAsync(cancellationToken))?.Trim()
            : _session.Reporter.Output.Prompt(new TextPrompt<string>("Token:").Secret());
}
