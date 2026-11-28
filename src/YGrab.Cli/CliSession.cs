// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using YGrab.Cli.Api;
using YGrab.Cli.Configuration;

namespace YGrab.Cli;

/// <summary>
/// What a command needs: the one HttpClient of the process and the reporter.
/// </summary>
internal sealed class CliSession(HttpClient http, Reporter reporter)
{
    public Reporter Reporter => reporter;

    public HttpClient Http => http;

    /// <returns>The client of the configured server, or null after a usage error.</returns>
    public ApiClient? Api()
    {
        CliConfig config;
        try
        {
            config = CliConfigStore.Effective();
        }
        catch (JsonException)
        {
            reporter.Usage($"The config file {CliConfigStore.FilePath} is not valid JSON.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(config.Token) || !TryParseUrl(config.Url, out var url))
        {
            reporter.Usage(
                $"Run ygrab login <url>, or set {CliConfigStore.URL_VARIABLE} and {CliConfigStore.TOKEN_VARIABLE}."
            );
            return null;
        }

        return new ApiClient(http, url, config.Token);
    }

    /// <summary>
    /// Accepts an absolute http or https URL and ends its path with a slash, so that a relative
    /// path keeps the folder of a proxy.
    /// </summary>
    public static bool TryParseUrl(string? text, [NotNullWhen(true)] out Uri? url)
    {
        url = null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed) || !IsHttp(parsed))
            return false;

        url = parsed.AbsolutePath.EndsWith('/')
            ? parsed
            : new UriBuilder(parsed) { Path = parsed.AbsolutePath + "/" }.Uri;
        return true;
    }

    private static bool IsHttp(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
