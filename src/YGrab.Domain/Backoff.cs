// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

internal static class Backoff
{
    internal const int MAX_ATTEMPTS = 3;

    internal static readonly TimeSpan Base = TimeSpan.FromMinutes(1);
    internal static readonly TimeSpan Cap = TimeSpan.FromMinutes(10);

    internal static TimeSpan Delay(int attempt) => Doubling.Delay(Base, Cap, attempt);
}
