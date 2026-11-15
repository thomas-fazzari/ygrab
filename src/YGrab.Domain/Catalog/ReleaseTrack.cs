// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <param name="VideoId">The audio-only video when the album has one, the music video otherwise.</param>
/// <param name="Seconds">The duration, or 0 when YouTube Music has none.</param>
public sealed record ReleaseTrack(
    VideoId VideoId,
    string Title,
    ImmutableArray<string> Artists,
    int Number,
    int Seconds,
    bool Explicit
);
