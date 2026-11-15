// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <param name="Error">Why the track is missing, and null in any other state.</param>
public sealed record JobTrack(ReleaseTrack Track, TrackState State, string? Error)
{
    /// <summary>
    /// Whether the file lacks the track, whatever the reason.
    /// </summary>
    public bool IsMissing => State is TrackState.Missing or TrackState.Refused;
}
