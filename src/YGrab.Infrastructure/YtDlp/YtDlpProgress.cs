// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Infrastructure.YtDlp;

/// <summary>
/// Reads the progress lines that the progress templates of yt-dlp print.
/// </summary>
internal static class YtDlpProgress
{
    // The video has two streams: the video stream fills the part up to VideoStreamEnd, then the
    // audio stream fills the part up to AudioStreamEnd and the merge takes the rest
    internal const double VideoStreamEnd = 0.90;
    internal const double AudioStreamEnd = 0.95;

    internal const string Prefix = "progress ";

    internal const string AudioTemplate =
        $"download:{Prefix}%(progress.downloaded_bytes)s %(progress.total_bytes,progress.total_bytes_estimate)s";

    internal const string VideoTemplate =
        $"download:{Prefix}%(info.vcodec)s %(progress.downloaded_bytes)s %(progress.total_bytes,progress.total_bytes_estimate)s";

    private const string NoVideoCodec = "none";

    /// <summary>
    /// Reads a line of the audio template, which has the downloaded and the total bytes.
    /// </summary>
    /// <param name="fraction">The downloaded part of the stream, from 0 to 1.</param>
    internal static bool TryParseAudio(string line, out double fraction)
    {
        fraction = 0;
        return line.StartsWith(Prefix, StringComparison.Ordinal)
            && TryParseBytes(line.AsSpan(Prefix.Length), out fraction);
    }

    /// <summary>
    /// Reads a line of the video template, which starts with the video codec of the stream.
    /// </summary>
    /// <remarks>
    /// The codec is <see cref="NoVideoCodec"/> for the audio stream. The result covers both streams,
    /// which yt-dlp downloads one after the other.
    /// </remarks>
    /// <param name="fraction">The downloaded part of both streams, from 0 to <see cref="AudioStreamEnd"/>.</param>
    internal static bool TryParseVideo(string line, out double fraction)
    {
        fraction = 0;
        if (!line.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        // "progress <video codec> <downloaded bytes> <total bytes>"
        var values = line.AsSpan(Prefix.Length);
        var space = values.IndexOf(' ');
        if (space < 0 || !TryParseBytes(values[(space + 1)..], out var stream))
        {
            return false;
        }

        fraction =
            values[..space] is NoVideoCodec
                ? VideoStreamEnd + ((AudioStreamEnd - VideoStreamEnd) * stream)
                : VideoStreamEnd * stream;
        return true;
    }

    // "<downloaded bytes> <total bytes>", where an unknown total is "NA" and an estimate is a float
    private static bool TryParseBytes(ReadOnlySpan<char> values, out double fraction)
    {
        fraction = 0;
        var space = values.IndexOf(' ');
        if (
            space < 0
            || !double.TryParse(
                values[..space],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var downloaded
            )
            || !double.TryParse(
                values[(space + 1)..],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var total
            )
            || !double.IsFinite(downloaded)
            || !double.IsFinite(total)
            || total <= 0
        )
        {
            return false;
        }

        fraction = Math.Clamp(downloaded / total, 0, 1);
        return true;
    }
}
