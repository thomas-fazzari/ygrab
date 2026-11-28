// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Infrastructure.Output;

/// <summary>
/// The names of the folder, the zip and the files that a job produces.
/// </summary>
internal static class ReleaseNaming
{
    internal const int MAX_BYTES = 200;

    internal const char INVALID_REPLACEMENT = '_';

    private const string ARTIST_SEPARATOR = ", ";

    // Characters that Windows, macOS or Linux reject in a file name
    private static readonly SearchValues<char> _invalid = SearchValues.Create(
        "\"*/:<>?\\|\0\u0001\u0002\u0003\u0004\u0005\u0006\u0007\b\t\n\u000b\f\r\u000e\u000f\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001a\u001b\u001c\u001d\u001e\u001f"
    );

    /// <summary>
    /// The folder of an album in its zip, which is also the name of the zip without its extension.
    /// </summary>
    public static string AlbumName(Release release)
    {
        ArgumentNullException.ThrowIfNull(release);

        var year = release.Year is { } value
            ? string.Create(CultureInfo.InvariantCulture, $" ({value})")
            : "";
        return Clean($"{Credit(release.Artists)} - {release.Title}{year}");
    }

    /// <param name="extension">The extension of the audio file, with its dot.</param>
    public static string EntryName(ReleaseTrack track, string extension)
    {
        ArgumentNullException.ThrowIfNull(track);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{track.Number:00} - {Clean(track.Title)}{extension}"
        );
    }

    /// <param name="extension">The extension of the audio file, with its dot.</param>
    public static string SingleName(ReleaseTrack track, string extension)
    {
        ArgumentNullException.ThrowIfNull(track);

        return Clean($"{Credit(track.Artists)} - {track.Title}") + extension;
    }

    /// <summary>
    /// The name of a video file: the channel and the title, then the video id.
    /// </summary>
    /// <remarks>
    /// The id comes after <see cref="Clean"/>, so the cut to <see cref="MAX_BYTES"/> never removes it.
    /// </remarks>
    /// <param name="extension">The extension of the video file, with its dot.</param>
    public static string VideoName(VideoId videoId, VideoInfo video, string extension)
    {
        ArgumentNullException.ThrowIfNull(videoId);
        ArgumentNullException.ThrowIfNull(video);

        var name = string.IsNullOrWhiteSpace(video.Channel)
            ? Clean(video.Title)
            : Clean($"{video.Channel} - {video.Title}");
        return $"{name} [{videoId.Value}]{extension}";
    }

    /// <summary>
    /// The artists on one line, as the names and the tags show them.
    /// </summary>
    public static string Credit(ImmutableArray<string> artists) =>
        string.Join(ARTIST_SEPARATOR, artists);

    /// <summary>
    /// Makes a name safe for a file system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each invalid character becomes <see cref="INVALID_REPLACEMENT"/>, and the name is cut to <see cref="MAX_BYTES"/> in
    /// UTF-8, on a character boundary.
    /// </para>
    /// <para>
    /// The dots and spaces at the end are trimmed, since Windows drops them.
    /// </para>
    /// </remarks>
    public static string Clean(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var cleaned = string.Create(name.Length, name, static (span, source) =>
        {
            source.AsSpan().CopyTo(span);
            int index;
            while ((index = span.IndexOfAny(_invalid)) >= 0)
            {
                span[index] = INVALID_REPLACEMENT;
            }
        });
        cleaned = CutToBytes(cleaned).Trim().TrimEnd('.', ' ');
        return cleaned.Length == 0 ? INVALID_REPLACEMENT.ToString() : cleaned;
    }

    private static string CutToBytes(string name)
    {
        var bytes = 0;
        var length = 0;
        foreach (var rune in name.EnumerateRunes())
        {
            bytes += rune.Utf8SequenceLength;
            if (bytes > MAX_BYTES)
                break;

            length += rune.Utf16SequenceLength;
        }

        return name[..length];
    }
}
