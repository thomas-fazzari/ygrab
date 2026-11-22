// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// The pause after each step, of a random length, so YouTube sees each step at a different time.
/// </summary>
public static class Pacing
{
    internal const int MinPauseSeconds = 5;
    internal const int MaxPauseSeconds = 15;

    public const int PauseChoices = MaxPauseSeconds - MinPauseSeconds + 1;

    public static TimeSpan Pause(int roll)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(roll);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(roll, PauseChoices);

        return TimeSpan.FromSeconds(MinPauseSeconds + roll);
    }
}
