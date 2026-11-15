// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json.Serialization;

namespace YGrab.Contracts.Jobs;

public enum JobModeCode
{
    [JsonStringEnumMemberName("audio")]
    Audio = 0,

    [JsonStringEnumMemberName("video")]
    Video = 1,
}
