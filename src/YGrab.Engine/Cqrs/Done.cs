// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine;

/// <summary>
/// Outcome of a command that has nothing to report.
/// </summary>
public readonly record struct Done
{
    public static ValueTask<Done> Completed => ValueTask.FromResult(new Done());
}
