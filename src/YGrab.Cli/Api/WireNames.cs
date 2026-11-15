// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using YGrab.Contracts;

namespace YGrab.Cli.Api;

/// <summary>
/// The JSON name of an enum value of the contracts, so the CLI shows the names the API uses.
/// </summary>
internal static class WireNames
{
    public static string Of<T>(T value)
        where T : struct, Enum
    {
        var typeInfo = (JsonTypeInfo<T>)ApiJsonContext.Default.GetTypeInfo(typeof(T))!;
        return JsonSerializer.Serialize(value, typeInfo).Trim('"');
    }
}
