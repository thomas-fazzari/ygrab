// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Web;

namespace YGrab.Domain;

/// <summary>
/// A YouTube link to one video.
/// </summary>
/// <remarks>
/// <para>
/// On the YouTube hosts, it accepts <see cref="WatchPath"/> with the id in
/// <see cref="VideoParameter"/>, and <see cref="ShortsPath"/> or <see cref="LivePath"/> followed
/// by the id. On <see cref="YouTubeHosts.ShortHost"/>, the path is the id.
/// </para>
/// <para>
/// Any other link is refused, such as a channel, a playlist or an album.
/// </para>
/// </remarks>
public static class VideoLink
{
    public const string InvalidMessage =
        "Paste a YouTube video link. An album link grabs audio only.";

    internal const string WatchPath = "/watch";
    internal const string VideoParameter = "v";
    internal const string ShortsPath = "/shorts/";
    internal const string LivePath = "/live/";

    public static Result<VideoId> Parse(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return Find(uri) is { } videoId
            ? new Result<VideoId>(videoId)
            : new ValidationError(InvalidMessage);
    }

    private static VideoId? Find(Uri uri)
    {
        if (!YouTubeHosts.IsHttp(uri))
        {
            return null;
        }

        if (YouTubeHosts.IsShort(uri))
        {
            return Id(uri.AbsolutePath.Trim('/'));
        }

        if (!YouTubeHosts.IsWeb(uri))
        {
            return null;
        }

        var path = uri.AbsolutePath;
        return path switch
        {
            WatchPath => Id(HttpUtility.ParseQueryString(uri.Query)[VideoParameter]),
            _ when path.StartsWith(ShortsPath, StringComparison.Ordinal) => Id(
                path[ShortsPath.Length..].TrimEnd('/')
            ),
            _ when path.StartsWith(LivePath, StringComparison.Ordinal) => Id(
                path[LivePath.Length..].TrimEnd('/')
            ),
            _ => null,
        };
    }

    private static VideoId? Id(string? id) =>
        id is not null && VideoId.Create(id).TryGetValue(out VideoId? videoId) ? videoId : null;
}
