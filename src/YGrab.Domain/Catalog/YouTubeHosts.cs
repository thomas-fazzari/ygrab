// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// The checks that the link parsers share.
/// </summary>
internal static class YouTubeHosts
{
    internal const string SHORT_HOST = "youtu.be";

    internal static bool IsHttp(Uri uri) => uri.IsAbsoluteUri && uri.Scheme is "https" or "http";

    /// <summary>
    /// Whether the host is <see cref="SHORT_HOST"/>, which has the video id as its path.
    /// </summary>
    internal static bool IsShort(Uri uri) => uri.Host is SHORT_HOST;

    internal static bool IsWeb(Uri uri) =>
        uri.Host is "music.youtube.com" or "www.youtube.com" or "youtube.com" or "m.youtube.com";
}
