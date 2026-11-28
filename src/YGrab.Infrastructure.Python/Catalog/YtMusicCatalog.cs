// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using CSnakes.Runtime;
using YGrab.Domain;
using YGrab.Engine.Diagnostics;
using YGrab.Engine.Jobs;
using YGrab.Infrastructure.Python.Extensions;

namespace YGrab.Infrastructure.Python.Catalog;

/// <summary>
/// Reads YouTube Music through ytmusicapi, in <c>Python/yt_music.py</c>.
/// </summary>
/// <remarks>
/// <para>
/// An exception that the module raises on purpose maps to its <see cref="RemoteFailure"/>.
/// Any other exception, or a result that does not have the shape that the bindings expect,
/// is unavailable. Its reason is <see cref="UNREADABLE_REASON"/> and the log keeps the details.
/// </para>
/// </remarks>
internal sealed partial class YtMusicCatalog(
    IPythonEnvironment python,
    ILogger<YtMusicCatalog> logger
) : ICatalog
{
    private const string UNREADABLE_REASON =
        "YouTube Music returned a page that YGrab cannot read.";

    // A call holds the GIL and blocks on the network, so it runs off the caller's thread
    // A cancelled caller stops waiting, and the call ends by itself
    public async ValueTask<CatalogLookup> FindAsync(
        MusicLink link,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(link);

        using var activity = YGrabTelemetry.Source.StartActivity("ytmusicapi");
        var lookup = await Task.Run(() => Find(link), cancellationToken)
            .WaitAsync(cancellationToken);
        if (lookup is RemoteFailure failure)
            YGrabTelemetry.MarkFailed(activity, YGrabTelemetry.ErrorType(failure));

        return lookup;
    }

    private CatalogLookup Find(MusicLink link)
    {
        try
        {
            var module = python.YtMusic();
            return link switch
            {
                AlbumLink album => Read(module, album.BrowseId, audioVideoId: null),
                AlbumPlaylistLink playlist => Read(
                    module,
                    module.AlbumOfPlaylist(playlist.PlaylistId.Value),
                    audioVideoId: null,
                    missing: "No YouTube Music album has this playlist."
                ),
                TrackLink track => ReadTrack(module, track),
            };
        }
        catch (PythonInvocationException exception)
        {
            return exception.ToRemoteFailure() is { } failure
                ? Failed(link, failure)
                : Unreadable(link, $"{exception.PythonExceptionType}: {exception.Reason}");
        }
        catch (InvalidCastException exception)
        {
            return Unreadable(link, exception.Message);
        }
    }

    private static CatalogLookup ReadTrack(IYtMusic module, TrackLink track)
    {
        var (browseId, audioVideoId) = module.AlbumOfTrack(track.VideoId.Value);
        return Read(
            module,
            browseId,
            audioVideoId,
            missing: "No YouTube Music album has this track."
        );
    }

    private static CatalogLookup Read(
        IYtMusic module,
        string? browseId,
        string? audioVideoId,
        string missing
    )
    {
        if (browseId is null)
            return new RemoteFailure.Unavailable(missing);

        if (BrowseId.Create(browseId) is not BrowseId parsed)
        {
            return new RemoteFailure.Unavailable(
                $"YouTube Music sent an album id that YGrab cannot read ({browseId})."
            );
        }

        if (audioVideoId is null)
            return Read(module, parsed, audioVideoId: null);

        return VideoId.Create(audioVideoId) is VideoId audio
            ? Read(module, parsed, audio)
            : new RemoteFailure.Unavailable(
                $"YouTube Music sent a video id that YGrab cannot read ({audioVideoId})."
            );
    }

    private static CatalogLookup Read(IYtMusic module, BrowseId browseId, VideoId? audioVideoId)
    {
        var album = module.Album(browseId.Value, audioVideoId?.Value);
        if (album.Tracks.Count == 0)
        {
            return new RemoteFailure.Unavailable(
                audioVideoId is null
                    ? "The album has no available track."
                    : "The album does not list this video."
            );
        }

        var tracks = ImmutableArray.CreateBuilder<ReleaseTrack>(album.Tracks.Count);
        foreach (var track in album.Tracks)
        {
            if (VideoId.Create(track.VideoId) is not VideoId id)
            {
                return new RemoteFailure.Unavailable(
                    $"YouTube Music returned the invalid video id {track.VideoId}."
                );
            }

            tracks.Add(
                new ReleaseTrack(
                    id,
                    track.Title,
                    [.. track.Artists],
                    (int)track.Number,
                    Durations.FromSeconds((int)track.Seconds),
                    track.Explicit
                )
            );
        }

        return Uri.TryCreate(album.CoverUrl, UriKind.Absolute, out var cover)
            ? new Release(
                browseId,
                album.Title,
                [.. album.Artists],
                int.TryParse(album.Year, CultureInfo.InvariantCulture, out var year) ? year : null,
                cover,
                (int)album.TrackCount,
                tracks.MoveToImmutable()
            )
            : new RemoteFailure.Unavailable(
                $"YouTube Music returned the invalid cover URL {album.CoverUrl}."
            );
    }

    private RemoteFailure Failed(MusicLink link, RemoteFailure failure)
    {
        LogLookupFailed(link, failure);
        return failure;
    }

    private RemoteFailure.Unavailable Unreadable(MusicLink link, string details)
    {
        LogUnreadable(link, details);
        return new RemoteFailure.Unavailable(UNREADABLE_REASON);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "YouTube Music lookup of {Link} failed: {Failure}"
    )]
    private partial void LogLookupFailed(MusicLink link, RemoteFailure failure);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "YouTube Music lookup of {Link} returned a result YGrab cannot read: {Details}"
    )]
    private partial void LogUnreadable(MusicLink link, string details);
}
