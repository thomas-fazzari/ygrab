// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Engine.Jobs;

namespace YGrab.Infrastructure.Covers;

/// <summary>
/// Fetches the cover of a release over HTTP.
/// </summary>
/// <remarks>
/// <para>
/// A cover is optional, so any failure except a cancellation is logged and gives null. That
/// includes a timeout and an open circuit of the resilience handler.
/// </para>
/// <para>
/// The URL comes from the catalog response, so only HTTPS URLs on the image hosts of YouTube
/// are fetched.
/// </para>
/// </remarks>
internal sealed partial class HttpCoverSource(
    IHttpClientFactory clients,
    ILogger<HttpCoverSource> logger
) : ICoverSource
{
    internal const string ClientName = "covers";

    /// <summary>
    /// The side of the square cover in pixels.
    /// </summary>
    /// <remarks>
    /// The original can reach 4000 pixels.
    /// </remarks>
    internal const int Size = 1200;

    // A cover of Size pixels weighs a few hundred kb
    internal const int MaxResponseBytes = 10 * 1024 * 1024;

    private const string GoogleUserContent = ".googleusercontent.com";

    private static readonly string[] _hosts = [GoogleUserContent, ".ggpht.com", ".ytimg.com"];

    public async ValueTask<Cover?> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        if (!IsImageHost(url))
        {
            LogHostRefused(logger, url);
            return null;
        }

        try
        {
            var client = clients.CreateClient(ClientName);
            return new Cover(await client.GetByteArrayAsync(Sized(url), cancellationToken));
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogFetchFailed(logger, exception, url);
            return null;
        }
    }

    // A googleusercontent URL ends with "=w544-h544-l90-rj": the size, the quality and JPEG
    internal static Uri Sized(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        var text = url.AbsoluteUri;
        var options = text.LastIndexOf('=', StringComparison.Ordinal);
        return url.Host.EndsWith(GoogleUserContent, StringComparison.Ordinal) && options > 0
            ? new Uri($"{text[..options]}=w{Size}-h{Size}-l90-rj")
            : url;
    }

    private static bool IsImageHost(Uri url) =>
        string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        && _hosts.Any(host => url.Host.EndsWith(host, StringComparison.OrdinalIgnoreCase));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not fetch the cover {Url}")]
    private static partial void LogFetchFailed(ILogger logger, Exception exception, Uri url);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Skipped the cover {Url}, which is not on an image host of YouTube"
    )]
    private static partial void LogHostRefused(ILogger logger, Uri url);
}
