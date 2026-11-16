// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Contracts.Diagnostics;

/// <summary>
/// The names of the events of the log stream, and the type of each payload.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><see cref="Snapshot"/>: the buffered lines as an array of <see cref="LogEntryDto"/>, oldest first, the first event of each stream.</description></item>
/// <item><description><see cref="Entry"/>: a <see cref="LogEntryDto"/>.</description></item>
/// </list>
/// </remarks>
public static class LogEventNames
{
    public const string Snapshot = "logs";
    public const string Entry = "log";
}
