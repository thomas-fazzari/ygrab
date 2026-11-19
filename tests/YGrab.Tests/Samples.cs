// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using YGrab.Contracts.Jobs;
using YGrab.Domain;

namespace YGrab.Tests;

internal static class Samples
{
    public const string AlbumBrowseId = "MPREb_album";
    public const string AlbumUrl = "https://music.youtube.com/browse/" + AlbumBrowseId;

    public const string VideoUrl = "https://www.youtube.com/watch?v=vid";

    public static readonly VideoInfo VideoDetails = new("Title", "Channel");

    public static readonly BrowseId Album = BrowseId.Create(AlbumBrowseId).ShouldSucceed();

    public static readonly RemoteFailure Transient = new RemoteFailure.Transient("timeout");

    public static VideoId Video(string id) => VideoId.Create(id).ShouldSucceed();

    public static AlbumLink AlbumLink() => new(Album);

    public static TrackLink TrackLink(string id) => new(Video(id));

    /// <summary>An album whose tracks have the video ids <c>v1</c>, <c>v2</c> and so on.</summary>
    public static Release Release(int tracks, string browseId = AlbumBrowseId) =>
        new(
            BrowseId.Create(browseId).ShouldSucceed(),
            "Album",
            ["Artist", "Guest"],
            2020,
            new Uri("https://example.com/cover"),
            tracks,
            [
                .. Enumerable
                    .Range(1, tracks)
                    .Select(number => new ReleaseTrack(
                        Video(string.Create(CultureInfo.InvariantCulture, $"v{number}")),
                        string.Create(CultureInfo.InvariantCulture, $"Title {number}"),
                        ["Artist"],
                        number,
                        200 + number,
                        number % 2 == 0
                    )),
            ]
        );

    public static JobRequest.Audio Audio(MusicLink link) => new(link);

    public static Job Queued(MusicLink link, bool force = false, DateTimeOffset? now = null) =>
        Job.Create(
            Guid.CreateVersion7(),
            new Uri(AlbumUrl),
            Audio(link),
            force,
            now ?? TestTime.Start
        );

    public static JobRequest.Video VideoRequest(
        string id = "vid",
        VideoQuality quality = VideoQuality.P1080
    ) => new(Video(id), quality);

    public static Job QueuedVideo(JobRequest.Video? request = null, DateTimeOffset? now = null) =>
        Job.Create(
            Guid.CreateVersion7(),
            new Uri(VideoUrl),
            request ?? VideoRequest(),
            force: false,
            now ?? TestTime.Start
        );

    /// <summary>A video job that is downloading and has no video file yet.</summary>
    public static Job DownloadingVideo(JobRequest.Video? request = null) =>
        QueuedVideo(request).Resume(TestTime.Start);

    public static Job Downloading(Release release, MusicLink? link = null) =>
        Queued(link ?? AlbumLink()).Resume(TestTime.Start).Resolve(release);

    public static Job Ready(Release release, MusicLink? link = null)
    {
        var job = Downloading(release, link);
        while (job.NextTrack is not null)
        {
            job = job.CompleteTrack();
        }

        return job.StartPackaging(TestTime.Start)
            .Finish("file.zip", 2048, enrichmentFailure: null, TestTime.Start);
    }

    /// <summary>The job after its next track fails every retry with a transient failure.</summary>
    public static Job Exhaust(Job job, DateTimeOffset now)
    {
        var current = job;
        for (var failure = 0; failure < Backoff.MaxAttempts; failure++)
        {
            current = current.Fail(Transient, now);
            if (current.State is JobState.Waiting waiting)
                current = current.Resume(waiting.RetryAt);
        }

        return current;
    }

    public static JobDto JobDto(
        Guid id,
        string? title = null,
        int? currentTrack = null,
        params ImmutableArray<TrackDto> tracks
    ) =>
        new(
            id,
            new Uri(AlbumUrl),
            JobModeCode.Audio,
            Quality: null,
            JobStateCode.Downloading,
            Reason: null,
            RetryAt: null,
            DuplicateOf: null,
            DateTimeOffset.UnixEpoch,
            FinishedAt: null,
            ExpiresAt: null,
            title,
            [],
            CoverUrl: null,
            FileName: null,
            FileSize: null,
            EnrichmentFailure: null,
            Percent: 0,
            tracks,
            currentTrack,
            CanFallBackToVideo: false
        );
}
