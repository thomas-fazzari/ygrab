// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// The pause after each step, of a random length, so YouTube sees each step at a different time.
/// </summary>
public static class Pacing
{
    internal const int MIN_PAUSE_SECONDS = 5;
    internal const int MAX_PAUSE_SECONDS = 15;

    public const int PAUSE_CHOICES = MAX_PAUSE_SECONDS - MIN_PAUSE_SECONDS + 1;

    public static TimeSpan Pause(int roll)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(roll);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(roll, PAUSE_CHOICES);

        return TimeSpan.FromSeconds(MIN_PAUSE_SECONDS + roll);
    }
}
