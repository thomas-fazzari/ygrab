// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Web;

namespace YGrab.Domain;

/// <summary>
/// A YouTube link to one video.
/// </summary>
/// <remarks>
/// <para>
/// On the YouTube hosts, it accepts <see cref="WATCH_PATH"/> with the id in
/// <see cref="VIDEO_PARAMETER"/>, and <see cref="SHORTS_PATH"/> or <see cref="LIVE_PATH"/> followed
/// by the id. On <see cref="YouTubeHosts.SHORT_HOST"/>, the path is the id.
/// </para>
/// <para>
/// Any other link is refused, such as a channel, a playlist or an album.
/// </para>
/// </remarks>
public static class VideoLink
{
    public const string INVALID_MESSAGE =
        "Video mode takes a link to one YouTube video. An album downloads as music.";

    internal const string WATCH_PATH = "/watch";
    internal const string VIDEO_PARAMETER = "v";
    internal const string SHORTS_PATH = "/shorts/";
    internal const string LIVE_PATH = "/live/";

    public static Result<VideoId> Parse(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return Find(uri) is { } videoId
            ? new Result<VideoId>(videoId)
            : new ValidationError(INVALID_MESSAGE);
    }

    private static VideoId? Find(Uri uri)
    {
        if (!YouTubeHosts.IsHttp(uri))
            return null;

        if (YouTubeHosts.IsShort(uri))
            return Id(uri.AbsolutePath.Trim('/'));

        if (!YouTubeHosts.IsWeb(uri))
            return null;

        var path = uri.AbsolutePath;
        return path switch
        {
            WATCH_PATH => Id(HttpUtility.ParseQueryString(uri.Query)[VIDEO_PARAMETER]),
            _ when path.StartsWith(SHORTS_PATH, StringComparison.Ordinal) => Id(
                path[SHORTS_PATH.Length..].TrimEnd('/')
            ),
            _ when path.StartsWith(LIVE_PATH, StringComparison.Ordinal) => Id(
                path[LIVE_PATH.Length..].TrimEnd('/')
            ),
            _ => null,
        };
    }

    private static VideoId? Id(string? id) =>
        id is not null && VideoId.Create(id) is VideoId videoId ? videoId : null;
}
