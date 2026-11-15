// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using YGrab.Domain;
using YGrab.Infrastructure.Database;
using YGrab.Infrastructure.Database.Extensions;

namespace YGrab.Infrastructure.Jobs;

internal sealed record JobRow(
    byte[] Id,
    string Url,
    string LinkKind,
    string LinkId,
    string? Quality,
    long Force,
    string Status,
    long CreatedAt,
    string? Release,
    string? Tracks,
    string? VideoTitle,
    string? VideoChannel,
    long Attempt,
    long? RetryAt,
    string? Error,
    string? FileName,
    byte[]? DuplicateOf,
    long? FinishedAt,
    string? EnrichmentFailure
)
{
    public const string SelectColumns = """
        id AS Id, url AS Url, link_kind AS LinkKind, link_id AS LinkId,
        quality AS Quality, force AS Force, status AS Status, created_at AS CreatedAt,
        release AS Release, tracks AS Tracks,
        video_title AS VideoTitle, video_channel AS VideoChannel, attempt AS Attempt,
        retry_at AS RetryAt, error AS Error, file_name AS FileName, duplicate_of AS DuplicateOf,
        finished_at AS FinishedAt, enrichment_failure AS EnrichmentFailure
        """;
}

internal static class JobRowMapper
{
    private const string AlbumKind = "album";
    private const string AlbumPlaylistKind = "album_playlist";
    private const string TrackKind = "track";
    private const string VideoKind = "video";

    private const string Quality720 = "720p";
    private const string Quality1080 = "1080p";
    private const string Quality1440 = "1440p";
    private const string Quality2160 = "2160p";
    private const string QualityBest = "best";
    private const string QualityAudio = "audio";

    private const string QueuedStatus = "queued";
    private const string ResolvingStatus = "resolving";
    private const string DownloadingStatus = "downloading";
    private const string WaitingStatus = "waiting";
    private const string PackagingStatus = "packaging";
    private const string ReadyStatus = "ready";
    private const string IncompleteStatus = "incomplete";
    private const string FailedStatus = "failed";
    private const string SkippedStatus = "skipped";
    private const string ExpiredStatus = "expired";

    private const string NoMatchFailure = "no_match";
    private const string UnreachableFailure = "unreachable";
    private const string ProviderErrorFailure = "provider_error";

    private const string PendingState = "pending";
    private const string DoneState = "done";
    private const string MissingState = "missing";
    private const string RefusedState = "refused";

    public static JobRow ToRow(Job job)
    {
        var (kind, linkId, quality) = Describe(job.Request);
        var (status, columns) = Describe(job.State);
        return new JobRow(
            GuidBlob.ToBlob(job.Id),
            job.Url.AbsoluteUri,
            kind,
            linkId,
            quality,
            job.Force ? 1 : 0,
            status,
            job.CreatedAt.ToUnixTimeMilliseconds(),
            job.Release is { } release ? SerializeRelease(release) : null,
            job.Resolved is null ? null : SerializeTracks(job.Tracks),
            job.KeptVideo?.Title,
            job.KeptVideo?.Channel,
            job.Attempt,
            columns.RetryAt?.ToUnixTimeMilliseconds(),
            columns.Error,
            columns.FileName,
            columns.DuplicateOf is { } duplicate ? GuidBlob.ToBlob(duplicate) : null,
            columns.FinishedAt?.ToUnixTimeMilliseconds(),
            job.EnrichmentFailure is { } failure ? Describe(failure) : null
        );
    }

    /// <exception cref="InvalidOperationException">The row holds an impossible combination.</exception>
    public static Job ToJob(JobRow row)
    {
        var columns = new StateColumns(
            row.RetryAt is { } retryAt ? DateTimeOffset.FromUnixTimeMilliseconds(retryAt) : null,
            row.Error,
            row.FileName,
            row.DuplicateOf is { } duplicate ? GuidBlob.FromBlob(duplicate) : null,
            row.FinishedAt is { } finishedAt
                ? DateTimeOffset.FromUnixTimeMilliseconds(finishedAt)
                : null
        );
        var state = ToState(row.Status, columns);

        return Describe(state).Columns != columns
            ? throw new InvalidOperationException(
                $"A {row.Status} job row has columns that its state does not use."
            )
            : Job.Restore(
                GuidBlob.FromBlob(row.Id),
                new Uri(row.Url, UriKind.Absolute),
                ToRequest(row.LinkKind, row.LinkId, row.Quality),
                row.Force == 1,
                DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAt),
                state,
                ToResolved(row.Release, row.Tracks),
                ToKeptVideo(row.VideoTitle, row.VideoChannel),
                checked((int)row.Attempt),
                row.EnrichmentFailure is { } failure ? ToEnrichmentFailure(failure) : null
            );
    }

    private static (string Kind, string LinkId, string? Quality) Describe(JobRequest request) =>
        request switch
        {
            JobRequest.Audio { Link: AlbumLink album } => (AlbumKind, album.BrowseId.Value, null),
            JobRequest.Audio { Link: AlbumPlaylistLink playlist } => (
                AlbumPlaylistKind,
                playlist.PlaylistId.Value,
                null
            ),
            JobRequest.Audio { Link: TrackLink track } => (TrackKind, track.VideoId.Value, null),
            JobRequest.Video video => (
                VideoKind,
                video.VideoId.Value,
                video.Quality switch
                {
                    VideoQuality.P720 => Quality720,
                    VideoQuality.P1080 => Quality1080,
                    VideoQuality.P1440 => Quality1440,
                    VideoQuality.P2160 => Quality2160,
                    VideoQuality.Best => QualityBest,
                    VideoQuality.Audio => QualityAudio,
                    _ => throw new InvalidOperationException("A video has an unknown quality."),
                }
            ),
        };

    private static (string Status, StateColumns Columns) Describe(JobState state) =>
        state switch
        {
            JobState.Queued => (QueuedStatus, StateColumns.None),
            JobState.Resolving => (ResolvingStatus, StateColumns.None),
            JobState.Downloading => (DownloadingStatus, StateColumns.None),
            JobState.Waiting waiting => (
                WaitingStatus,
                StateColumns.None with
                {
                    RetryAt = waiting.RetryAt,
                    Error = waiting.Reason,
                }
            ),
            JobState.Packaging => (PackagingStatus, StateColumns.None),
            JobState.Ready ready => (
                ReadyStatus,
                StateColumns.None with
                {
                    FileName = ready.FileName,
                    FinishedAt = ready.FinishedAt,
                }
            ),
            JobState.Incomplete incomplete => (
                IncompleteStatus,
                StateColumns.None with
                {
                    FileName = incomplete.FileName,
                    FinishedAt = incomplete.FinishedAt,
                }
            ),
            JobState.Failed failed => (
                FailedStatus,
                StateColumns.None with
                {
                    Error = failed.Reason,
                    FinishedAt = failed.FinishedAt,
                }
            ),
            JobState.Skipped skipped => (
                SkippedStatus,
                StateColumns.None with
                {
                    DuplicateOf = skipped.DuplicateOf,
                    FinishedAt = skipped.FinishedAt,
                }
            ),
            JobState.Expired expired => (
                ExpiredStatus,
                StateColumns.None with
                {
                    FinishedAt = expired.FinishedAt,
                }
            ),
        };

    private static JobState ToState(string status, StateColumns columns) =>
        status switch
        {
            QueuedStatus => new JobState.Queued(),
            ResolvingStatus => new JobState.Resolving(),
            DownloadingStatus => new JobState.Downloading(),
            WaitingStatus => new JobState.Waiting(
                Required(columns.RetryAt, "retry_at"),
                Required(columns.Error, "error")
            ),
            PackagingStatus => new JobState.Packaging(),
            ReadyStatus => new JobState.Ready(
                Required(columns.FileName, "file_name"),
                Required(columns.FinishedAt, "finished_at")
            ),
            IncompleteStatus => new JobState.Incomplete(
                Required(columns.FileName, "file_name"),
                Required(columns.FinishedAt, "finished_at")
            ),
            FailedStatus => new JobState.Failed(
                Required(columns.Error, "error"),
                Required(columns.FinishedAt, "finished_at")
            ),
            SkippedStatus => new JobState.Skipped(
                Required(columns.DuplicateOf, "duplicate_of"),
                Required(columns.FinishedAt, "finished_at")
            ),
            ExpiredStatus => new JobState.Expired(Required(columns.FinishedAt, "finished_at")),
            _ => throw new InvalidOperationException($"A job row has the status {status}."),
        };

    private static JobRequest ToRequest(string kind, string id, string? quality)
    {
        if (kind is not VideoKind && quality is not null)
        {
            throw new InvalidOperationException($"A {kind} job row has a quality.");
        }

        return kind switch
        {
            VideoKind => new JobRequest.Video(
                VideoId.Create(id).GetStoredValue(),
                ToQuality(
                    quality ?? throw new InvalidOperationException("A video row has no quality.")
                )
            ),
            AlbumKind => new JobRequest.Audio(new AlbumLink(BrowseId.Create(id).GetStoredValue())),
            AlbumPlaylistKind => new JobRequest.Audio(
                new AlbumPlaylistLink(PlaylistId.Create(id).GetStoredValue())
            ),
            TrackKind => new JobRequest.Audio(new TrackLink(VideoId.Create(id).GetStoredValue())),
            _ => throw new InvalidOperationException($"A job row has the link kind {kind}."),
        };
    }

    private static VideoQuality ToQuality(string quality) =>
        quality switch
        {
            Quality720 => VideoQuality.P720,
            Quality1080 => VideoQuality.P1080,
            Quality1440 => VideoQuality.P1440,
            Quality2160 => VideoQuality.P2160,
            QualityBest => VideoQuality.Best,
            QualityAudio => VideoQuality.Audio,
            _ => throw new InvalidOperationException($"A video row has the quality {quality}."),
        };

    private static string Describe(EnrichmentFailure failure) =>
        failure switch
        {
            EnrichmentFailure.NoMatch => NoMatchFailure,
            EnrichmentFailure.Unreachable => UnreachableFailure,
            EnrichmentFailure.ProviderError => ProviderErrorFailure,
            _ => throw new InvalidOperationException("A job has an unknown enrichment failure."),
        };

    private static EnrichmentFailure ToEnrichmentFailure(string failure) =>
        failure switch
        {
            NoMatchFailure => EnrichmentFailure.NoMatch,
            UnreachableFailure => EnrichmentFailure.Unreachable,
            ProviderErrorFailure => EnrichmentFailure.ProviderError,
            _ => throw new InvalidOperationException(
                $"A job row has the enrichment failure {failure}."
            ),
        };

    private static VideoInfo? ToKeptVideo(string? title, string? channel)
    {
        if (title is not null)
        {
            return new VideoInfo(title, channel);
        }

        return channel is null
            ? null
            : throw new InvalidOperationException("A job row has a channel and no title.");
    }

    private static string SerializeRelease(Release release) =>
        JsonSerializer.Serialize(
            new ReleaseDocument(
                release.BrowseId.Value,
                release.Title,
                release.Artists,
                release.Year,
                release.CoverUrl.AbsoluteUri,
                release.TrackCount
            ),
            StoredJson.Default.ReleaseDocument
        );

    private static string SerializeTracks(ImmutableArray<JobTrack> tracks) =>
        JsonSerializer.Serialize(
            [
                .. tracks.Select(track => new TrackDocument(
                    track.Track.VideoId.Value,
                    track.Track.Title,
                    track.Track.Artists,
                    track.Track.Number,
                    track.Track.Seconds,
                    track.Track.Explicit,
                    track.State switch
                    {
                        TrackState.Pending => PendingState,
                        TrackState.Done => DoneState,
                        TrackState.Missing => MissingState,
                        TrackState.Refused => RefusedState,
                        _ => throw new InvalidOperationException("A track has an unknown state."),
                    },
                    track.Error
                )),
            ],
            StoredJson.Default.ImmutableArrayTrackDocument
        );

    private static JobRelease? ToResolved(string? releaseJson, string? tracksJson)
    {
        if (releaseJson is null && tracksJson is null)
        {
            return null;
        }

        if (releaseJson is null || tracksJson is null)
        {
            throw new InvalidOperationException("A job row has only one of release and tracks.");
        }

        var document = JsonSerializer.Deserialize(releaseJson, StoredJson.Default.ReleaseDocument)
            ?? throw new InvalidOperationException("A job row has a null release.");
        var tracks = ToTracks(tracksJson);
        var release = new Release(
            BrowseId.Create(document.BrowseId).GetStoredValue(),
            document.Title,
            document.Artists,
            document.Year,
            new Uri(document.CoverUrl, UriKind.Absolute),
            document.TrackCount,
            [.. tracks.Select(track => track.Track)]
        );
        return JobRelease.Restore(release, tracks);
    }

    private static ImmutableArray<JobTrack> ToTracks(string json)
    {
        var documents = JsonSerializer.Deserialize(
            json,
            StoredJson.Default.ImmutableArrayTrackDocument
        );
        return documents.IsDefault
            ? throw new InvalidOperationException("A job row has null tracks.")
            :
            [
                .. documents.Select(document => new JobTrack(
                    new ReleaseTrack(
                        VideoId.Create(document.VideoId).GetStoredValue(),
                        document.Title,
                        document.Artists,
                        document.Number,
                        document.Seconds,
                        document.Explicit
                    ),
                    document.State switch
                    {
                        PendingState => TrackState.Pending,
                        DoneState => TrackState.Done,
                        MissingState => TrackState.Missing,
                        RefusedState => TrackState.Refused,
                        _ => throw new InvalidOperationException(
                            $"A stored track has the state {document.State}."
                        ),
                    },
                    document.Error
                )),
            ];
    }

    private static T Required<T>(T? value, string column)
        where T : struct =>
        value ?? throw new InvalidOperationException($"The column {column} is null.");

    private static string Required(string? value, string column) =>
        value ?? throw new InvalidOperationException($"The column {column} is null.");

    private readonly record struct StateColumns(
        DateTimeOffset? RetryAt,
        string? Error,
        string? FileName,
        Guid? DuplicateOf,
        DateTimeOffset? FinishedAt
    )
    {
        public static StateColumns None => default;
    }
}
