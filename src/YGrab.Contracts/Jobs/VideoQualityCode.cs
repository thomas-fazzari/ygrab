// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json.Serialization;

namespace YGrab.Contracts.Jobs;

public enum VideoQualityCode
{
    [JsonStringEnumMemberName("720p")]
    P720 = 0,

    [JsonStringEnumMemberName("1080p")]
    P1080 = 1,

    [JsonStringEnumMemberName("1440p")]
    P1440 = 2,

    [JsonStringEnumMemberName("2160p")]
    P2160 = 3,

    [JsonStringEnumMemberName("best")]
    Best = 4,

    [JsonStringEnumMemberName("audio")]
    Audio = 5,
}
