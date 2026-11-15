// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json.Serialization;

namespace YGrab.Infrastructure.Jobs;

internal sealed record ReleaseDocument(
    string BrowseId,
    string Title,
    ImmutableArray<string> Artists,
    int? Year,
    string CoverUrl,
    int TrackCount
);

internal sealed record TrackDocument(
    string VideoId,
    string Title,
    ImmutableArray<string> Artists,
    int Number,
    int Seconds,
    bool Explicit,
    string State,
    string? Error
);

/// <summary>
/// The JSON of the <c>release</c> and <c>tracks</c> columns, with snake case names.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ReleaseDocument))]
[JsonSerializable(typeof(ImmutableArray<TrackDocument>))]
internal sealed partial class StoredJson : JsonSerializerContext;
