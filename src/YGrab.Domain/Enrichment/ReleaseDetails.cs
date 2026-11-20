// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// What a provider knows about a release, on top of the YouTube Music tags.
/// </summary>
/// <remarks>
/// <para>
/// A missing value is null: the provider does not have it, or its value is empty.
/// </para>
/// <para>
/// <see cref="Tracks"/> holds one entry per available track of the release, keyed by
/// <see cref="ReleaseTrack.Number"/>.
/// </para>
/// </remarks>
/// <param name="DiscTotal">The number of discs of the whole release, even for a job of one track.</param>
public sealed record ReleaseDetails(
    string? Label,
    Barcode? Barcode,
    DateOnly? ReleaseDate,
    ReleaseType? ReleaseType,
    int DiscTotal,
    ImmutableDictionary<int, TrackDetails> Tracks
);

/// <param name="NumberInDisc">The position of the track on its disc.</param>
/// <param name="DiscTrackCount">The number of tracks on its disc.</param>
public sealed record TrackDetails(Isrc? Isrc, int Disc, int NumberInDisc, int DiscTrackCount);

public enum ReleaseType
{
    Album = 0,
    Ep = 1,
    SingleRelease = 2,
    Compilation = 3,
}
