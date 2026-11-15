// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <param name="TrackCount">The tracks of the album, including those that are not available.</param>
/// <param name="Tracks">The available tracks. For a track link, only the track of the link.</param>
public sealed record Release(
    BrowseId BrowseId,
    string Title,
    ImmutableArray<string> Artists,
    int? Year,
    Uri CoverUrl,
    int TrackCount,
    ImmutableArray<ReleaseTrack> Tracks
);
