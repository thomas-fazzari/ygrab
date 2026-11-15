// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

internal static class Doubling
{
    /// <summary>
    /// The delay that starts at <paramref name="initial"/>, doubles with each step and stops at
    /// <paramref name="cap"/>.
    /// </summary>
    internal static TimeSpan Delay(TimeSpan initial, TimeSpan cap, int steps)
    {
        var delay = initial;
        for (var step = 0; step < steps && delay < cap; step++)
        {
            delay *= 2;
        }

        return delay < cap ? delay : cap;
    }
}
