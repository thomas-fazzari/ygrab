// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using CSnakes.Runtime;
using YGrab.Domain;
using YGrab.Engine.Jobs;

namespace YGrab.Infrastructure.Python.Catalog;

/// <summary>
/// Reads YouTube Music through ytmusicapi, in <c>Python/yt_music.py</c>.
/// </summary>
/// <remarks>
/// <para>
/// The Python module raises <c>TransientError</c>, <c>RateLimitedError</c> and
/// <c>UnavailableError</c>. This adapter maps them to <see cref="RemoteFailure"/> by class name.
/// </para>
/// <para>
/// A result that does not have the shape that the bindings expect is unavailable.
/// </para>
/// </remarks>
internal sealed partial class YtMusicCatalog(
    IPythonEnvironment python,
    ILogger<YtMusicCatalog> logger
) : ICatalog
{
    private const string RateLimitedErrorName = "RateLimitedError";
    private const string TransientErrorName = "TransientError";

    // A call holds the GIL and blocks on the network, so it runs off the caller's thread
    // A cancelled caller stops waiting, and the call ends by itself
    public async ValueTask<CatalogLookup> FindAsync(
        MusicLink link,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(link);

        return await Task.Run(() => Find(link), cancellationToken).WaitAsync(cancellationToken);
    }

    internal static RemoteFailure Classify(PythonInvocationException exception)
    {
        var reason = exception.InnerException?.Message ?? exception.Message;
        return exception.PythonExceptionType switch
        {
            RateLimitedErrorName => new RemoteFailure.RateLimited(reason),
            TransientErrorName => new RemoteFailure.Transient(reason),
            var type => new RemoteFailure.Unavailable($"{type}: {reason}"),
        };
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
            return Failed(link, Classify(exception));
        }
        catch (InvalidCastException exception)
        {
            return Failed(
                link,
                new RemoteFailure.Unavailable(
                    $"YouTube Music returned an unexpected result: {exception.Message}"
                )
            );
        }
    }

    private static CatalogLookup ReadTrack(IYtMusic module, TrackLink track)
    {
        var (browseId, audioVideoId) = module.AlbumOfTrack(track.VideoId.Value);
        return Read(
            module,
            browseId,
            audioVideoId,
            missing: "No YouTube Music album track matches this video. Paste the album link, or grab the video with the audio quality to keep its sound only."
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
        {
            return new RemoteFailure.Unavailable(missing);
        }

        return BrowseId.Create(browseId).TryGetValue(out BrowseId? parsed)
            ? Read(module, parsed, audioVideoId)
            : new RemoteFailure.Unavailable(
                $"YouTube Music returned the invalid album id {browseId}."
            );
    }

    private static CatalogLookup Read(IYtMusic module, BrowseId browseId, string? audioVideoId)
    {
        var album = module.Album(browseId.Value, audioVideoId);
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
            if (!VideoId.Create(track.VideoId).TryGetValue(out VideoId? id))
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
                    (int)track.Seconds,
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

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "YouTube Music lookup of {Link} failed: {Failure}"
    )]
    private partial void LogLookupFailed(MusicLink link, RemoteFailure failure);
}
