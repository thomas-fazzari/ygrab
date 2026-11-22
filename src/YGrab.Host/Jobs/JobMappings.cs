// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Contracts;
using YGrab.Contracts.Jobs;
using YGrab.Domain;

namespace YGrab.Host.Jobs;

internal static class JobMappings
{
    extension(Job job)
    {
        public JobDto ToDto(Retention retention) =>
            new(
                job.Id.Value,
                job.Url,
                ToCode(job.Request),
                job.Request is JobRequest.Video video ? ToCode(video.Quality) : null,
                ToCode(job.State),
                ReasonOf(job.State),
                job.RetryAt,
                (job.State as JobState.Skipped)?.DuplicateOf.Value,
                job.CreatedAt,
                job.QueueRank,
                job.FinishedAt,
                retention.ExpiresAt(job),
                job.Release?.Title ?? job.KeptVideo?.Title,
                ArtistsOf(job),
                job.Release?.CoverUrl,
                (job.State as JobState.WithFile)?.FileName,
                (job.State as JobState.WithFile)?.FileSize,
                job.EnrichmentFailure is { } failure ? ToCode(failure) : null,
                job.Percent,
                [.. job.Tracks.Select(MapTrack)],
                job.IsActive ? job.NextTrack?.Track.Number : null,
                job.CanFallBackToVideo
            );
    }

    extension(Cooldown cooldown)
    {
        public CooldownDto ToDto() => new(cooldown.Strikes, cooldown.Until, cooldown.Reason);
    }

    extension(JobModeCode mode)
    {
        public JobMode ToDomain() =>
            mode switch
            {
                JobModeCode.Audio => JobMode.Audio,
                JobModeCode.Video => JobMode.Video,
                _ => throw UnknownCode(mode),
            };
    }

    extension(VideoQualityCode quality)
    {
        public VideoQuality ToDomain() =>
            quality switch
            {
                VideoQualityCode.P720 => VideoQuality.P720,
                VideoQualityCode.P1080 => VideoQuality.P1080,
                VideoQualityCode.P1440 => VideoQuality.P1440,
                VideoQualityCode.P2160 => VideoQuality.P2160,
                VideoQualityCode.Best => VideoQuality.Best,
                VideoQualityCode.Audio => VideoQuality.Audio,
                _ => throw UnknownCode(quality),
            };
    }

    private static TrackDto MapTrack(JobTrack track) =>
        new(track.Track.Number, track.Track.Title, ToCode(track.State), track.Error);

    private static ImmutableArray<string> ArtistsOf(Job job)
    {
        if (job.Release is { } release)
            return release.Artists;

        return job.KeptVideo?.Channel is { } channel ? [channel] : [];
    }

    private static string? ReasonOf(JobState state) =>
        state switch
        {
            JobState.Waiting waiting => waiting.Reason,
            JobState.Failed failed => failed.Reason,
            JobState.Skipped skipped => $"Already grabbed by job {skipped.DuplicateOf}",
            _ => null,
        };

    private static JobModeCode ToCode(JobRequest request) =>
        request switch
        {
            JobRequest.Audio => JobModeCode.Audio,
            JobRequest.Video => JobModeCode.Video,
        };

    private static JobStateCode ToCode(JobState state) =>
        state switch
        {
            JobState.Queued => JobStateCode.Queued,
            JobState.Resolving => JobStateCode.Resolving,
            JobState.Downloading => JobStateCode.Downloading,
            JobState.Waiting => JobStateCode.Waiting,
            JobState.Packaging => JobStateCode.Packaging,
            JobState.Ready => JobStateCode.Ready,
            JobState.Incomplete => JobStateCode.Incomplete,
            JobState.Failed => JobStateCode.Failed,
            JobState.Skipped => JobStateCode.Skipped,
            JobState.Expired => JobStateCode.Expired,
        };

    private static TrackStateCode ToCode(TrackState state) =>
        state switch
        {
            TrackState.Pending => TrackStateCode.Pending,
            TrackState.Done => TrackStateCode.Done,
            TrackState.Missing => TrackStateCode.Missing,
            TrackState.Refused => TrackStateCode.Refused,
            _ => throw UnknownCode(state),
        };

    private static VideoQualityCode ToCode(VideoQuality quality) =>
        quality switch
        {
            VideoQuality.P720 => VideoQualityCode.P720,
            VideoQuality.P1080 => VideoQualityCode.P1080,
            VideoQuality.P1440 => VideoQualityCode.P1440,
            VideoQuality.P2160 => VideoQualityCode.P2160,
            VideoQuality.Best => VideoQualityCode.Best,
            VideoQuality.Audio => VideoQualityCode.Audio,
            _ => throw UnknownCode(quality),
        };

    private static EnrichmentFailureCode ToCode(EnrichmentFailure failure) =>
        failure switch
        {
            EnrichmentFailure.NoMatch => EnrichmentFailureCode.NoMatch,
            EnrichmentFailure.Unreachable => EnrichmentFailureCode.Unreachable,
            EnrichmentFailure.ProviderError => EnrichmentFailureCode.ProviderError,
            _ => throw UnknownCode(failure),
        };

    private static InvalidOperationException UnknownCode<T>(T value)
        where T : struct, Enum => new($"The {typeof(T).Name} value {value} has no code.");
}
