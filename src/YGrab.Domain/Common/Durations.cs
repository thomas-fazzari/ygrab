// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// Converts the whole seconds of a source, where 0 means unknown, to and from a duration.
/// </summary>
public static class Durations
{
    /// <returns>The duration, or null when <paramref name="seconds"/> is 0 or less.</returns>
    public static TimeSpan? FromSeconds(int seconds) =>
        seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;

    /// <returns>The whole seconds, or 0 when <paramref name="duration"/> is null.</returns>
    public static int ToSeconds(TimeSpan? duration) =>
        duration is { } known ? (int)known.TotalSeconds : 0;
}
