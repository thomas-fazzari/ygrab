// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// Where a track of a <see cref="Job"/> is.
/// </summary>
/// <remarks>
/// <see cref="Missing"/> and <see cref="Refused"/> both mean that the file lacks the track. They
/// differ by the reason, which the refusal guard of the job needs.
/// </remarks>
public enum TrackState
{
    Pending = 0,
    Done = 1,

    /// <summary>
    /// YouTube says that the track is unavailable.
    /// </summary>
    Missing = 2,

    /// <summary>
    /// Every retry of a transient failure failed.
    /// </summary>
    Refused = 3,
}
