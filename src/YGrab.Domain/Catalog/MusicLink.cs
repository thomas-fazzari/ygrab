// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Web;

namespace YGrab.Domain;

/// <summary>
/// A YouTube or YouTube Music link to an album or a track.
/// </summary>
public closed record MusicLink
{
    public const string INVALID_MESSAGE = "Paste a YouTube Music album or track link.";

    private const string BROWSE_PATH = "/browse/";

    public static Result<MusicLink> Parse(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        return Find(uri) is { } link
            ? new Result<MusicLink>(link)
            : new ValidationError(INVALID_MESSAGE);
    }

    private static MusicLink? Find(Uri uri)
    {
        if (!YouTubeHosts.IsHttp(uri))
            return null;

        if (YouTubeHosts.IsShort(uri))
            return Track(uri.AbsolutePath.Trim('/'));

        if (!YouTubeHosts.IsWeb(uri))
            return null;

        var query = HttpUtility.ParseQueryString(uri.Query);
        return uri.AbsolutePath switch
        {
            "/watch" => Track(query["v"]),
            "/playlist" => AlbumPlaylist(query["list"]),
            var path when path.StartsWith(BROWSE_PATH, StringComparison.Ordinal) => Album(
                path[BROWSE_PATH.Length..].TrimEnd('/')
            ),
            _ => null,
        };
    }

    private static TrackLink? Track(string? id) =>
        id is not null && VideoId.Create(id) is VideoId videoId ? new TrackLink(videoId) : null;

    private static AlbumPlaylistLink? AlbumPlaylist(string? id) =>
        id is not null && PlaylistId.Create(id) is PlaylistId playlistId
            ? new AlbumPlaylistLink(playlistId)
            : null;

    private static AlbumLink? Album(string id) =>
        BrowseId.Create(id) is BrowseId browseId ? new AlbumLink(browseId) : null;
}

/// <summary>
/// A YouTube Music album page.
/// </summary>
public sealed record AlbumLink(BrowseId BrowseId) : MusicLink;

/// <summary>
/// The audio playlist of an album.
/// </summary>
public sealed record AlbumPlaylistLink(PlaylistId PlaylistId) : MusicLink;

public sealed record TrackLink(VideoId VideoId) : MusicLink;
