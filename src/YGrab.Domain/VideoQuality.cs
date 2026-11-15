// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// The resolution cap of a video download, or <see cref="Audio"/> for its audio stream only.
/// </summary>
/// <remarks>
/// <see cref="P1080"/> is the zero value, so it is the default of a new request.
/// </remarks>
public enum VideoQuality
{
    P1080 = 0,
    P720 = 1,
    P1440 = 2,
    P2160 = 3,
    Best = 4,
    Audio = 5,
}
