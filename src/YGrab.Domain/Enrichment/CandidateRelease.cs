// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// A release that a provider offers, to compare with the release of YouTube Music.
/// </summary>
/// <param name="Artist">The main artist.</param>
/// <param name="TrackCount">The tracks of the release, across all its discs.</param>
/// <param name="Tracks">The tracks in flat order across the discs. Empty until the provider listed them.</param>
public sealed record CandidateRelease(
    string Title,
    string Artist,
    int TrackCount,
    ImmutableArray<CandidateTrack> Tracks
);

/// <param name="Duration">The duration, or null when the provider has none.</param>
/// <param name="Disc">The disc number, from 1.</param>
/// <param name="NumberInDisc">The position of the track on its disc, from 1.</param>
public sealed record CandidateTrack(
    string Title,
    TimeSpan? Duration,
    bool Explicit,
    int Disc,
    int NumberInDisc,
    Isrc? Isrc
);
