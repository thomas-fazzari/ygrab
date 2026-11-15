// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json.Serialization;

namespace YGrab.Infrastructure.Deezer;

/// <summary>
/// The error that Deezer puts in the body of a 200 answer.
/// </summary>
/// <param name="Code">The Deezer error code, where 4 means that the quota is exceeded.</param>
internal sealed record DeezerError(int Code, string? Message);

internal sealed record DeezerArtist(string Name);

internal sealed record DeezerAlbumHit(long Id, string Title, DeezerArtist? Artist, int NbTracks);

internal sealed record DeezerSearchBody(ImmutableArray<DeezerAlbumHit> Data, DeezerError? Error);

internal sealed record DeezerTrack(
    string Title,
    int Duration,
    bool ExplicitLyrics,
    int DiskNumber,
    int TrackPosition,
    string? Isrc
);

internal sealed record DeezerTracksBody(ImmutableArray<DeezerTrack> Data, DeezerError? Error);

internal sealed record DeezerAlbumBody(
    string? Label,
    string? Upc,
    string? ReleaseDate,
    string? RecordType,
    DeezerError? Error
);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(DeezerSearchBody))]
[JsonSerializable(typeof(DeezerTracksBody))]
[JsonSerializable(typeof(DeezerAlbumBody))]
internal sealed partial class DeezerJson : JsonSerializerContext;
