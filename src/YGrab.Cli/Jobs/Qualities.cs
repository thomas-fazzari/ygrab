// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Cli.Api;
using YGrab.Contracts.Jobs;

namespace YGrab.Cli.Jobs;

internal static class Qualities
{
    public static string[] Names { get; } =
    [.. Enum.GetValues<VideoQualityCode>().Select(WireNames.Of)];

    private static readonly Dictionary<string, VideoQualityCode> _byName =
        Enum.GetValues<VideoQualityCode>().ToDictionary(WireNames.Of, StringComparer.Ordinal);

    public static VideoQualityCode? Parse(string name) =>
        _byName.TryGetValue(name, out var quality) ? quality : null;
}
