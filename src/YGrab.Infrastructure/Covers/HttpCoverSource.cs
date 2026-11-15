// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Engine.Jobs;

namespace YGrab.Infrastructure.Covers;

/// <summary>
/// Fetches the cover of a release over HTTP.
/// </summary>
/// <remarks>
/// A cover is optional, so any failure except a cancellation is logged and gives null. That
/// includes a timeout and an open circuit of the resilience handler.
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

    public async ValueTask<Cover?> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
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
        return url.Host.EndsWith(".googleusercontent.com", StringComparison.Ordinal) && options > 0
            ? new Uri($"{text[..options]}=w{Size}-h{Size}-l90-rj")
            : url;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not fetch the cover {Url}")]
    private static partial void LogFetchFailed(ILogger logger, Exception exception, Uri url);
}
