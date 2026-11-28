// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Tests.Domain;

public sealed class JobTests
{
    private static readonly DateTimeOffset _now = TestTime.Start;

    private static readonly RemoteFailure _transient = Samples.Transient;

    [Fact]
    public void A_rate_limit_queues_the_job_and_keeps_its_tracks_and_attempt()
    {
        var job = Samples.Downloading(Samples.Release(3)).CompleteTrack();
        job = job.Fail(_transient, _now).Resume(_now + Backoff.Delay(0));
        job.Attempt.Should().Be(1);

        var limited = job.Fail(new RemoteFailure.RateLimited("429"), _now);

        limited.State.Should().BeOfType<JobState.Queued>();
        limited.Attempt.Should().Be(1);
        limited
            .Tracks.Select(track => track.State)
            .Should()
            .Equal(TrackState.Done, TrackState.Pending, TrackState.Pending);
    }

    [Fact]
    public void A_transient_failure_waits_with_a_backoff_until_the_attempts_run_out()
    {
        var job = Samples.Downloading(Samples.Release(3));

        var first = job.Fail(_transient, _now);

        first.State.Should().Be(new JobState.Waiting(_now + Backoff.Delay(0), "timeout"));
        first.Attempt.Should().Be(1);
        first.Invoking(waiting => waiting.Resume(_now)).Should().Throw<InvalidOperationException>();

        var given = Samples.Exhaust(job, _now);

        given.State.Should().BeOfType<JobState.Downloading>();
        given.Attempt.Should().Be(0);
        given
            .Tracks[0]
            .Should()
            .Match<JobTrack>(track =>
                track.State == TrackState.Refused && track.Error == "timeout"
            );
        given.NextTrack!.Track.Number.Should().Be(2);
    }

    [Fact]
    public void An_unavailable_track_goes_missing_and_an_unavailable_album_fails()
    {
        var downloading = Samples
            .Downloading(Samples.Release(2))
            .Fail(new RemoteFailure.Unavailable("private"), _now);
        var resolving = Samples
            .Queued(Samples.AlbumLink())
            .Resume(_now)
            .Fail(new RemoteFailure.Unavailable("gone"), _now);

        downloading.Tracks[0].State.Should().Be(TrackState.Missing);
        downloading.State.Should().BeOfType<JobState.Downloading>();
        resolving.State.Should().Be(new JobState.Failed("gone", _now));
    }

    [Fact]
    public void Two_tracks_in_a_row_that_exhaust_their_retries_with_nothing_downloaded_fail_the_job()
    {
        var first = Samples.Exhaust(Samples.Downloading(Samples.Release(4)), _now);
        first.State.Should().BeOfType<JobState.Downloading>();

        var second = Samples.Exhaust(first, _now);

        second.State.Should().Be(new JobState.Failed(Job.REFUSED_MESSAGE, _now));
    }

    [Fact]
    public void An_unavailable_track_before_a_track_that_exhausts_its_retries_does_not_fail_the_job()
    {
        var job = Samples
            .Downloading(Samples.Release(4))
            .Fail(new RemoteFailure.Unavailable("private"), _now);

        var next = Samples.Exhaust(job, _now);

        next.State.Should().BeOfType<JobState.Downloading>();
        next.Tracks.Select(track => track.State)
            .Should()
            .Equal(TrackState.Missing, TrackState.Refused, TrackState.Pending, TrackState.Pending);
    }

    [Fact]
    public void A_downloaded_track_keeps_the_job_going_after_a_track_exhausts_its_retries()
    {
        var job = Samples.Downloading(Samples.Release(4)).CompleteTrack();

        var next = Samples.Exhaust(Samples.Exhaust(job, _now), _now);

        next.State.Should().BeOfType<JobState.Downloading>();
        next.NextTrack!.Track.Number.Should().Be(4);
    }

    [Fact]
    public void Packaging_ends_ready_incomplete_or_failed_by_what_was_downloaded()
    {
        var release = Samples.Release(2);
        var downloaded = Samples.Downloading(release).CompleteTrack().CompleteTrack();
        var mixed = Samples
            .Downloading(release)
            .CompleteTrack()
            .Fail(new RemoteFailure.Unavailable("private"), _now);
        var none = Samples
            .Downloading(release)
            .Fail(new RemoteFailure.Unavailable("a"), _now)
            .Fail(new RemoteFailure.Unavailable("b"), _now);

        downloaded
            .StartPackaging(_now)
            .Finish("a.zip", 2048, enrichmentFailure: null, _now)
            .State.Should()
            .Be(new JobState.Ready("a.zip", 2048, _now));
        mixed
            .StartPackaging(_now)
            .Finish("a.zip", 2048, enrichmentFailure: null, _now)
            .State.Should()
            .Be(new JobState.Incomplete("a.zip", 2048, _now));
        none.StartPackaging(_now)
            .State.Should()
            .Be(new JobState.Failed(Job.NOTHING_DOWNLOADED_MESSAGE, _now));
    }

    [Fact]
    public void A_retry_keeps_the_tracks_whose_files_exist_and_queues_the_others_from_the_start()
    {
        var incomplete = Samples
            .Downloading(Samples.Release(4))
            .CompleteTrack()
            .CompleteTrack()
            .Fail(new RemoteFailure.Unavailable("private"), _now)
            .CompleteTrack()
            .StartPackaging(_now)
            .Finish("a.zip", 2048, EnrichmentFailure.NoMatch, _now);

        var retried = incomplete.Retry(new HashSet<VideoId> { Samples.Video("v2") });

        retried.State.Should().BeOfType<JobState.Queued>();
        retried
            .Tracks.Select(track => track.State)
            .Should()
            .Equal(TrackState.Done, TrackState.Pending, TrackState.Pending, TrackState.Done);
        retried.Tracks.Should().OnlyContain(track => track.Error == null);
        retried.Attempt.Should().Be(0);
        retried.EnrichmentFailure.Should().BeNull();
        retried.Percent.Should().Be(50);
        Samples
            .Ready(Samples.Release(2))
            .Invoking(ready => ready.Retry(new HashSet<VideoId>()))
            .Should()
            .Throw<InvalidOperationException>();
    }

    [Fact]
    public void An_enrichment_failure_belongs_to_a_packaged_audio_job_only()
    {
        var packaging = Samples
            .Downloading(Samples.Release(1))
            .CompleteTrack()
            .StartPackaging(_now);
        var video = Samples
            .DownloadingVideo()
            .CompleteVideo(Samples.VideoDetails)
            .StartPackaging(_now);
        var queued = Samples.Queued(Samples.AlbumLink());

        var finished = packaging.Finish("a.zip", 2048, EnrichmentFailure.Unreachable, _now);

        finished.EnrichmentFailure.Should().Be(EnrichmentFailure.Unreachable);
        video
            .Invoking(job => job.Finish("v.mp4", 2048, EnrichmentFailure.NoMatch, _now))
            .Should()
            .Throw<InvalidOperationException>();
        FluentActions
            .Invoking(() =>
                Job.Restore(
                    queued.Id,
                    queued.Url,
                    queued.Request,
                    force: false,
                    queued.CreatedAt,
                    queued.QueueRank,
                    queued.State,
                    resolved: null,
                    keptVideo: null,
                    attempt: 0,
                    EnrichmentFailure.NoMatch
                )
            )
            .Should()
            .Throw<InvalidOperationException>();
    }

    [Fact]
    public void Progress_never_goes_back_and_ignores_a_value_that_is_not_a_number()
    {
        var job = Samples.Downloading(Samples.Release(2));

        var half = job.Progress(0.5);
        var back = half.Progress(0.1);

        half.Percent.Should().Be(25);
        back.Should().BeSameAs(half);
        half.Progress(double.NaN).Should().BeSameAs(half);
        half.Progress(double.PositiveInfinity).Should().BeSameAs(half);
        half.Progress(7).Percent.Should().Be(50);
        job.CompleteTrack().Percent.Should().Be(50);
    }

    [Fact]
    public void A_track_job_does_not_cover_an_album_link_but_an_album_job_covers_its_track()
    {
        var release = Samples.Release(2);
        var trackJob = Samples.Downloading(release, Samples.TrackLink("v1"));
        var albumJob = Samples.Downloading(release);

        trackJob.Covers(Samples.Audio(Samples.TrackLink("v1"))).Should().BeTrue();
        trackJob.Covers(Samples.Audio(Samples.AlbumLink())).Should().BeFalse();
        albumJob.Covers(Samples.Audio(Samples.AlbumLink())).Should().BeTrue();
        albumJob.Covers(Samples.Audio(Samples.TrackLink("v2"))).Should().BeTrue();
        albumJob.Covers(Samples.Audio(Samples.TrackLink("other"))).Should().BeFalse();
    }

    [Fact]
    public void A_missing_track_and_a_failed_skipped_or_expired_job_cover_nothing()
    {
        var release = Samples.Release(2);
        var missing = Samples
            .Downloading(release)
            .Fail(new RemoteFailure.Unavailable("private"), _now);
        var failed = Samples.Downloading(release).Abort("broken", _now);
        var skipped = Samples
            .Queued(Samples.AlbumLink())
            .Resume(_now)
            .Skip(new JobId(Guid.Empty), _now);
        var expired = Samples.Ready(release).Expire();

        missing.Covers(Samples.Audio(Samples.TrackLink("v1"))).Should().BeFalse();
        missing.Covers(Samples.Audio(Samples.TrackLink("v2"))).Should().BeTrue();
        foreach (var finished in new[] { failed, skipped, expired })
        {
            finished.Covers(Samples.Audio(Samples.AlbumLink())).Should().BeFalse();
            finished.Covers(Samples.Downloading(release)).Should().BeFalse();
        }
    }

    [Fact]
    public void An_album_job_covers_a_resolved_track_job_of_its_album_and_a_track_job_covers_only_a_track_job()
    {
        var release = Samples.Release(2);
        var album = Samples.Downloading(release);
        var track = Samples.Downloading(
            release with
            {
                Tracks = [release.Tracks[0]],
            },
            Samples.TrackLink("v1")
        );

        album.Covers(track).Should().BeTrue();
        track.Covers(album).Should().BeFalse();
        track
            .Covers(
                Samples.Downloading(
                    release with
                    {
                        Tracks = [release.Tracks[0]],
                    },
                    Samples.TrackLink("v1")
                )
            )
            .Should()
            .BeTrue();
    }

    [Fact]
    public void A_track_link_to_a_music_video_is_covered_through_its_audio_track()
    {
        var release = Samples.Release(2);
        var audio = release.Tracks[0];
        var video = Samples.TrackLink("omv");
        var track = Samples.Downloading(release with { Tracks = [audio] }, video);
        var album = Samples.Downloading(release);

        track.Covers(Samples.Audio(video)).Should().BeTrue();
        track.Covers(Samples.Audio(new TrackLink(audio.VideoId))).Should().BeTrue();
        album.Covers(track).Should().BeTrue();
        album.Covers(Samples.Audio(video)).Should().BeFalse();
    }

    [Fact]
    public void An_album_playlist_job_and_an_album_job_of_the_same_release_cover_each_other()
    {
        var release = Samples.Release(2);
        var playlist = new AlbumPlaylistLink(PlaylistId.Create("OLAK5uy_x").ShouldSucceed());
        var byPlaylist = Samples.Downloading(release, playlist);
        var byAlbum = Samples.Downloading(release);

        byPlaylist.Covers(byAlbum).Should().BeTrue();
        byAlbum.Covers(byPlaylist).Should().BeTrue();
        byPlaylist.Covers(Samples.Audio(Samples.AlbumLink())).Should().BeTrue();
        byAlbum.Covers(Samples.Audio(playlist)).Should().BeFalse();
    }

    [Fact]
    public void A_video_job_downloads_once_then_packages_without_resolving()
    {
        var queued = Samples.QueuedVideo();

        var pending = queued.Resume(_now);
        var kept = pending.Progress(0.5).CompleteVideo(Samples.VideoDetails);
        var finished = kept.StartPackaging(_now)
            .Finish("video.mp4", 2048, enrichmentFailure: null, _now);

        pending.State.Should().BeOfType<JobState.Downloading>();
        pending.NextStep().Should().Be(new JobStep.DownloadVideo(Samples.VideoRequest()));
        pending.Progress(0.5).Percent.Should().BePositive();
        kept.NextStep().Should().BeOfType<JobStep.Package>();
        kept.KeptVideo.Should().Be(Samples.VideoDetails);
        kept.Invoking(job => job.CompleteVideo(Samples.VideoDetails))
            .Should()
            .Throw<InvalidOperationException>();
        pending
            .Invoking(job => job.StartPackaging(_now))
            .Should()
            .Throw<InvalidOperationException>();
        finished.State.Should().Be(new JobState.Ready("video.mp4", 2048, _now));
    }

    [Fact]
    public void A_requeued_video_job_that_kept_its_file_goes_back_to_downloading_then_packages()
    {
        var kept = Samples.DownloadingVideo().CompleteVideo(Samples.VideoDetails);

        var resumed = kept.Requeue().Resume(_now);

        resumed.State.Should().BeOfType<JobState.Downloading>();
        resumed.NextStep().Should().BeOfType<JobStep.Package>();
        resumed.KeptVideo.Should().Be(Samples.VideoDetails);
    }

    [Fact]
    public void A_video_job_fails_when_the_video_is_unavailable_or_the_retries_run_out()
    {
        var job = Samples.DownloadingVideo();

        var unavailable = job.Fail(new RemoteFailure.Unavailable("private"), _now);
        var exhausted = Samples.Exhaust(job, _now);
        var limited = job.Fail(new RemoteFailure.RateLimited("429"), _now);

        unavailable.State.Should().Be(new JobState.Failed("private", _now));
        exhausted.State.Should().Be(new JobState.Failed("timeout", _now));
        limited.State.Should().BeOfType<JobState.Queued>();
    }

    [Fact]
    public void A_video_job_covers_a_video_request_of_the_same_id_and_quality_only()
    {
        var video = Samples.QueuedVideo();
        var expired = Samples
            .DownloadingVideo()
            .CompleteVideo(Samples.VideoDetails)
            .StartPackaging(_now)
            .Finish("video.mp4", 2048, enrichmentFailure: null, _now)
            .Expire();
        var track = Samples.Queued(Samples.TrackLink("vid"));

        video.Covers(Samples.VideoRequest()).Should().BeTrue();
        video.Covers(Samples.VideoRequest(quality: VideoQuality.P720)).Should().BeFalse();
        video.Covers(Samples.VideoRequest("other")).Should().BeFalse();
        video.Covers(Samples.Audio(Samples.TrackLink("vid"))).Should().BeFalse();
        track.Covers(Samples.VideoRequest()).Should().BeFalse();
        expired.Covers(Samples.VideoRequest()).Should().BeFalse();
    }

    [Fact]
    public void A_requeue_applies_to_the_steps_that_a_stop_interrupts_and_not_to_a_wait()
    {
        var downloading = Samples.Downloading(Samples.Release(2));
        var waiting = downloading.Fail(_transient, _now);

        downloading.Requeue().State.Should().BeOfType<JobState.Queued>();
        waiting.Requeue().Should().BeSameAs(waiting);
    }
}
