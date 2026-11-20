// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Infrastructure.YtDlp;

/// <summary>
/// Classifies the last error line of yt-dlp, and the warning lines that report a failed call.
/// </summary>
internal static class YtDlpErrors
{
    private const string NoErrorLine = "The download stopped without a reason.";
    private const string IdEnd = ": ";
    private const string WarningPrefix = "WARNING: ";
    private const string RetryMark = ". Retrying";

    private static readonly SearchValues<string> _rateLimited = SearchValues.Create(
        [
            "HTTP Error 429",
            "Too Many Requests",
            "not a bot",
            "try again later",
            "rate-limited",
            "rate limited",
            "captcha",
        ],
        StringComparison.OrdinalIgnoreCase
    );

    private static readonly SearchValues<string> _unavailable = SearchValues.Create(
        [
            "Video unavailable",
            "Private video",
            "is private",
            "not available in your country",
            "has been removed",
            "members-only",
            "confirm your age",
            "terminated",
            "will begin in",
            "Premieres in",
            // ffmpeg fails the same way on the same streams, so a retry would download them again for nothing
            "Postprocessing:",
        ],
        StringComparison.OrdinalIgnoreCase
    );

    /// <summary>
    /// Anything that is not a rate limit or a final refusal is transient: a 403, a timeout or no
    /// error line at all.
    /// </summary>
    internal static RemoteFailure Classify(string? lastErrorLine)
    {
        if (string.IsNullOrWhiteSpace(lastErrorLine))
            return new RemoteFailure.Transient(NoErrorLine);

        var line = lastErrorLine.AsSpan();
        var reason = ReasonOf(lastErrorLine);
        if (line.ContainsAny(_rateLimited))
            return new RemoteFailure.RateLimited(reason);

        return line.ContainsAny(_unavailable)
            ? new RemoteFailure.Unavailable(reason)
            : new RemoteFailure.Transient(reason);
    }

    /// <summary>
    /// Classifies a warning line of yt-dlp that reports a failed call to YouTube.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Such a warning either announces a retry, as in
    /// <c>WARNING: [youtube] abc: The read operation timed out. Retrying (1/3)...</c>, or reports
    /// a rate limit. yt-dlp never retries a 429, and it reports one on a call it can do without as
    /// <c>WARNING: [youtube] abc: HTTP Error 429: Too Many Requests</c>.
    /// </para>
    /// </remarks>
    /// <returns>The failure that the line reports, or null for any other line.</returns>
    internal static RemoteFailure? WarningCause(string line)
    {
        if (!line.StartsWith(WarningPrefix, StringComparison.Ordinal))
            return null;

        var failure = Classify(line[WarningPrefix.Length..]);
        if (failure is RemoteFailure.RateLimited)
            return failure;

        return line.Contains(RetryMark, StringComparison.Ordinal) ? failure : null;
    }

    // yt-dlp starts the line with the extractor and the video id, such as "[youtube] abc: "
    private static string ReasonOf(string line)
    {
        var extractorEnd = line.StartsWith('[', StringComparison.Ordinal)
            ? line.IndexOf(']', StringComparison.Ordinal)
            : -1;
        if (extractorEnd < 0)
            return line;

        var idEnd = line.IndexOf(IdEnd, extractorEnd, StringComparison.Ordinal);
        return idEnd < 0 ? line : line[(idEnd + IdEnd.Length)..];
    }
}
