// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json.Serialization;

namespace YGrab.Contracts.Diagnostics;

public sealed record LogEntryDto(
    DateTimeOffset Time,
    LogLevelCode Level,
    string Source,
    string Message
);

public enum LogLevelCode
{
    [JsonStringEnumMemberName("trace")]
    Trace = 0,

    [JsonStringEnumMemberName("debug")]
    Debug = 1,

    [JsonStringEnumMemberName("information")]
    Information = 2,

    [JsonStringEnumMemberName("warning")]
    Warning = 3,

    [JsonStringEnumMemberName("error")]
    Error = 4,

    [JsonStringEnumMemberName("critical")]
    Critical = 5,
}
