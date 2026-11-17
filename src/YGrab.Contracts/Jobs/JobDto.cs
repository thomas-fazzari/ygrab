// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace YGrab.Contracts.Jobs;

/// <summary>
/// A job as the clients see it.
/// </summary>
/// <param name="Quality">The resolution cap of a video job, and null for an audio job.</param>
/// <param name="Reason">Why the job waits, failed or was skipped.</param>
/// <param name="RetryAt">When a waiting job tries again.</param>
/// <param name="DuplicateOf">The job that already grabbed the link of a skipped job.</param>
/// <param name="ExpiresAt">When the job loses its files under the current retention, and null for a job that keeps none.</param>
/// <param name="Title">The album, the track or the video, once the job knows it.</param>
/// <param name="Artists">The artists of the release, or the channel of a video.</param>
/// <param name="CoverUrl">The cover of the release, once the job knows it.</param>
/// <param name="FileName">The name that a download saves, once the job is ready.</param>
/// <param name="Percent">The progress from 0 to 100.</param>
/// <param name="CurrentTrack">The number of the track that the job downloads next.</param>
public sealed record JobDto(
    Guid Id,
    Uri Url,
    JobModeCode Mode,
    VideoQualityCode? Quality,
    JobStateCode State,
    string? Reason,
    DateTimeOffset? RetryAt,
    Guid? DuplicateOf,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? ExpiresAt,
    string? Title,
    ImmutableArray<string> Artists,
    Uri? CoverUrl,
    string? FileName,
    EnrichmentFailureCode? EnrichmentFailure,
    int Percent,
    ImmutableArray<TrackDto> Tracks,
    int? CurrentTrack
);

/// <param name="Reason">Why the file lacks the track.</param>
public sealed record TrackDto(int Number, string Title, TrackStateCode State, string? Reason);

public enum JobStateCode
{
    [JsonStringEnumMemberName("queued")]
    Queued = 0,

    [JsonStringEnumMemberName("resolving")]
    Resolving = 1,

    [JsonStringEnumMemberName("downloading")]
    Downloading = 2,

    [JsonStringEnumMemberName("waiting")]
    Waiting = 3,

    [JsonStringEnumMemberName("packaging")]
    Packaging = 4,

    [JsonStringEnumMemberName("ready")]
    Ready = 5,

    [JsonStringEnumMemberName("incomplete")]
    Incomplete = 6,

    [JsonStringEnumMemberName("failed")]
    Failed = 7,

    [JsonStringEnumMemberName("skipped")]
    Skipped = 8,

    [JsonStringEnumMemberName("expired")]
    Expired = 9,
}

public enum TrackStateCode
{
    [JsonStringEnumMemberName("pending")]
    Pending = 0,

    [JsonStringEnumMemberName("done")]
    Done = 1,

    [JsonStringEnumMemberName("missing")]
    Missing = 2,

    [JsonStringEnumMemberName("refused")]
    Refused = 3,
}

public enum EnrichmentFailureCode
{
    [JsonStringEnumMemberName("no_match")]
    NoMatch = 0,

    [JsonStringEnumMemberName("unreachable")]
    Unreachable = 1,

    [JsonStringEnumMemberName("provider_error")]
    ProviderError = 2,
}
