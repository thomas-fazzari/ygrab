// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Reads the album behind a link from YouTube Music.
/// </summary>
public interface ICatalog
{
    /// <returns>
    /// The release. For a <see cref="TrackLink"/>, it lists only the track of the link.
    /// </returns>
    ValueTask<CatalogLookup> FindAsync(MusicLink link, CancellationToken cancellationToken);
}

public readonly union CatalogLookup(Release, RemoteFailure);
