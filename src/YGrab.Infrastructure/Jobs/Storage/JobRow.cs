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
    double QueueRank,
    string? Release,
    string? Tracks,
    string? VideoTitle,
    string? VideoChannel,
    long Attempt,
    long? RetryAt,
    string? Error,
    string? FileName,
    long? FileSize,
    byte[]? DuplicateOf,
    long? FinishedAt,
    string? EnrichmentFailure
)
{
    public const string SELECT_COLUMNS = """
        id AS Id, url AS Url, link_kind AS LinkKind, link_id AS LinkId,
        quality AS Quality, force AS Force, status AS Status, created_at AS CreatedAt,
        queue_rank AS QueueRank, release AS Release, tracks AS Tracks,
        video_title AS VideoTitle, video_channel AS VideoChannel, attempt AS Attempt,
        retry_at AS RetryAt, error AS Error, file_name AS FileName, file_size AS FileSize,
        duplicate_of AS DuplicateOf, finished_at AS FinishedAt,
        enrichment_failure AS EnrichmentFailure
        """;
}

internal static class JobRowMapper
{
    private const string ALBUM_KIND = "album";
    private const string ALBUM_PLAYLIST_KIND = "album_playlist";
    private const string TRACK_KIND = "track";
    private const string VIDEO_KIND = "video";

    private const string QUALITY720 = "720p";
    private const string QUALITY1080 = "1080p";
    private const string QUALITY1440 = "1440p";
    private const string QUALITY2160 = "2160p";
    private const string QUALITY_BEST = "best";
    private const string QUALITY_AUDIO = "audio";

    private const string QUEUED_STATUS = "queued";
    private const string RESOLVING_STATUS = "resolving";
    private const string DOWNLOADING_STATUS = "downloading";
    private const string WAITING_STATUS = "waiting";
    private const string PACKAGING_STATUS = "packaging";
    private const string READY_STATUS = "ready";
    private const string INCOMPLETE_STATUS = "incomplete";
    private const string FAILED_STATUS = "failed";
    private const string SKIPPED_STATUS = "skipped";
    private const string EXPIRED_STATUS = "expired";

    private const string NO_MATCH_FAILURE = "no_match";
    private const string UNREACHABLE_FAILURE = "unreachable";
    private const string PROVIDER_ERROR_FAILURE = "provider_error";

    private const string PENDING_STATE = "pending";
    private const string DONE_STATE = "done";
    private const string MISSING_STATE = "missing";
    private const string REFUSED_STATE = "refused";

    public static JobRow ToRow(Job job)
    {
        var (kind, linkId, quality) = Describe(job.Request);
        var (status, columns) = Describe(job.State);
        return new JobRow(
            GuidBlob.ToBlob(job.Id.Value),
            job.Url.AbsoluteUri,
            kind,
            linkId,
            quality,
            job.Force ? 1 : 0,
            status,
            job.CreatedAt.ToUnixTimeMilliseconds(),
            job.QueueRank,
            job.Release is { } release ? SerializeRelease(release) : null,
            job.Resolved is null ? null : SerializeTracks(job.Tracks),
            job.KeptVideo?.Title,
            job.KeptVideo?.Channel,
            job.Attempt,
            columns.RetryAt?.ToUnixTimeMilliseconds(),
            columns.Error,
            columns.FileName,
            columns.FileSize,
            columns.DuplicateOf is { } duplicate ? GuidBlob.ToBlob(duplicate.Value) : null,
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
            row.FileSize,
            row.DuplicateOf is { } duplicate ? new JobId(GuidBlob.FromBlob(duplicate)) : null,
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
                new JobId(GuidBlob.FromBlob(row.Id)),
                new Uri(row.Url, UriKind.Absolute),
                ToRequest(row.LinkKind, row.LinkId, row.Quality),
                row.Force == 1,
                DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAt),
                row.QueueRank,
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
            JobRequest.Audio { Link: AlbumLink album } => (ALBUM_KIND, album.BrowseId.Value, null),
            JobRequest.Audio { Link: AlbumPlaylistLink playlist } => (
                ALBUM_PLAYLIST_KIND,
                playlist.PlaylistId.Value,
                null
            ),
            JobRequest.Audio { Link: TrackLink track } => (TRACK_KIND, track.VideoId.Value, null),
            JobRequest.Video video => (
                VIDEO_KIND,
                video.VideoId.Value,
                video.Quality switch
                {
                    VideoQuality.P720 => QUALITY720,
                    VideoQuality.P1080 => QUALITY1080,
                    VideoQuality.P1440 => QUALITY1440,
                    VideoQuality.P2160 => QUALITY2160,
                    VideoQuality.Best => QUALITY_BEST,
                    VideoQuality.Audio => QUALITY_AUDIO,
                    _ => throw new InvalidOperationException("A video has an unknown quality."),
                }
            ),
        };

    private static (string Status, StateColumns Columns) Describe(JobState state) =>
        state switch
        {
            JobState.Queued => (QUEUED_STATUS, StateColumns.None),
            JobState.Resolving => (RESOLVING_STATUS, StateColumns.None),
            JobState.Downloading => (DOWNLOADING_STATUS, StateColumns.None),
            JobState.Waiting waiting => (
                WAITING_STATUS,
                StateColumns.None with
                {
                    RetryAt = waiting.RetryAt,
                    Error = waiting.Reason,
                }
            ),
            JobState.Packaging => (PACKAGING_STATUS, StateColumns.None),
            JobState.Ready ready => (
                READY_STATUS,
                StateColumns.None with
                {
                    FileName = ready.FileName,
                    FileSize = ready.FileSize,
                    FinishedAt = ready.FinishedAt,
                }
            ),
            JobState.Incomplete incomplete => (
                INCOMPLETE_STATUS,
                StateColumns.None with
                {
                    FileName = incomplete.FileName,
                    FileSize = incomplete.FileSize,
                    FinishedAt = incomplete.FinishedAt,
                }
            ),
            JobState.Failed failed => (
                FAILED_STATUS,
                StateColumns.None with
                {
                    Error = failed.Reason,
                    FinishedAt = failed.FinishedAt,
                }
            ),
            JobState.Skipped skipped => (
                SKIPPED_STATUS,
                StateColumns.None with
                {
                    DuplicateOf = skipped.DuplicateOf,
                    FinishedAt = skipped.FinishedAt,
                }
            ),
            JobState.Expired expired => (
                EXPIRED_STATUS,
                StateColumns.None with
                {
                    FinishedAt = expired.FinishedAt,
                }
            ),
        };

    private static JobState ToState(string status, StateColumns columns) =>
        status switch
        {
            QUEUED_STATUS => new JobState.Queued(),
            RESOLVING_STATUS => new JobState.Resolving(),
            DOWNLOADING_STATUS => new JobState.Downloading(),
            WAITING_STATUS => new JobState.Waiting(
                Required(columns.RetryAt, "retry_at"),
                Required(columns.Error, "error")
            ),
            PACKAGING_STATUS => new JobState.Packaging(),
            READY_STATUS => new JobState.Ready(
                Required(columns.FileName, "file_name"),
                columns.FileSize,
                Required(columns.FinishedAt, "finished_at")
            ),
            INCOMPLETE_STATUS => new JobState.Incomplete(
                Required(columns.FileName, "file_name"),
                columns.FileSize,
                Required(columns.FinishedAt, "finished_at")
            ),
            FAILED_STATUS => new JobState.Failed(
                Required(columns.Error, "error"),
                Required(columns.FinishedAt, "finished_at")
            ),
            SKIPPED_STATUS => new JobState.Skipped(
                Required(columns.DuplicateOf, "duplicate_of"),
                Required(columns.FinishedAt, "finished_at")
            ),
            EXPIRED_STATUS => new JobState.Expired(Required(columns.FinishedAt, "finished_at")),
            _ => throw new InvalidOperationException($"A job row has the status {status}."),
        };

    private static JobRequest ToRequest(string kind, string id, string? quality)
    {
        if (kind is not VIDEO_KIND && quality is not null)
            throw new InvalidOperationException($"A {kind} job row has a quality.");

        return kind switch
        {
            VIDEO_KIND => new JobRequest.Video(
                VideoId.Create(id).GetStoredValue(),
                ToQuality(
                    quality ?? throw new InvalidOperationException("A video row has no quality.")
                )
            ),
            ALBUM_KIND => new JobRequest.Audio(new AlbumLink(BrowseId.Create(id).GetStoredValue())),
            ALBUM_PLAYLIST_KIND => new JobRequest.Audio(
                new AlbumPlaylistLink(PlaylistId.Create(id).GetStoredValue())
            ),
            TRACK_KIND => new JobRequest.Audio(new TrackLink(VideoId.Create(id).GetStoredValue())),
            _ => throw new InvalidOperationException($"A job row has the link kind {kind}."),
        };
    }

    private static VideoQuality ToQuality(string quality) =>
        quality switch
        {
            QUALITY720 => VideoQuality.P720,
            QUALITY1080 => VideoQuality.P1080,
            QUALITY1440 => VideoQuality.P1440,
            QUALITY2160 => VideoQuality.P2160,
            QUALITY_BEST => VideoQuality.Best,
            QUALITY_AUDIO => VideoQuality.Audio,
            _ => throw new InvalidOperationException($"A video row has the quality {quality}."),
        };

    private static string Describe(EnrichmentFailure failure) =>
        failure switch
        {
            EnrichmentFailure.NoMatch => NO_MATCH_FAILURE,
            EnrichmentFailure.Unreachable => UNREACHABLE_FAILURE,
            EnrichmentFailure.ProviderError => PROVIDER_ERROR_FAILURE,
            _ => throw new InvalidOperationException("A job has an unknown enrichment failure."),
        };

    private static EnrichmentFailure ToEnrichmentFailure(string failure) =>
        failure switch
        {
            NO_MATCH_FAILURE => EnrichmentFailure.NoMatch,
            UNREACHABLE_FAILURE => EnrichmentFailure.Unreachable,
            PROVIDER_ERROR_FAILURE => EnrichmentFailure.ProviderError,
            _ => throw new InvalidOperationException(
                $"A job row has the enrichment failure {failure}."
            ),
        };

    private static VideoInfo? ToKeptVideo(string? title, string? channel)
    {
        if (title is not null)
            return new VideoInfo(title, channel);

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
                    Durations.ToSeconds(track.Track.Duration),
                    track.Track.Explicit,
                    track.State switch
                    {
                        TrackState.Pending => PENDING_STATE,
                        TrackState.Done => DONE_STATE,
                        TrackState.Missing => MISSING_STATE,
                        TrackState.Refused => REFUSED_STATE,
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
            return null;

        if (releaseJson is null || tracksJson is null)
            throw new InvalidOperationException("A job row has only one of release and tracks.");

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
                        Durations.FromSeconds(document.Seconds),
                        document.Explicit
                    ),
                    document.State switch
                    {
                        PENDING_STATE => TrackState.Pending,
                        DONE_STATE => TrackState.Done,
                        MISSING_STATE => TrackState.Missing,
                        REFUSED_STATE => TrackState.Refused,
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
        long? FileSize,
        JobId? DuplicateOf,
        DateTimeOffset? FinishedAt
    )
    {
        public static StateColumns None => default;
    }
}
