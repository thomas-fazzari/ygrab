// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Engine;

public sealed class AdvanceJobsTests
{
    private const int TrackCount = 4;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_rate_limit_queues_the_job_and_starts_a_cooldown_that_stops_every_call()
    {
        var rig = new JobRig(Samples.Release(TrackCount));
        rig.Audio.Script(null, null, new RemoteFailure.RateLimited("429"));
        await rig.Start.HandleAsync(new StartJob.Command(Samples.AlbumUrl), Cancellation);

        var limited = await rig.AdvanceAsync(steps: 4);

        var until = TestTime.Start + Cooldown.Base;
        limited.Value.Should().Be(new AdvanceJobs.WaitUntil(until, NotBefore: TestTime.Start));
        var job = rig.Jobs.List(1)[0];
        job.State.Should().BeOfType<JobState.Queued>();
        job.Tracks.Select(track => track.State)
            .Should()
            .Equal(TrackState.Done, TrackState.Done, TrackState.Pending, TrackState.Pending);
        rig.Cooldown.Current.Until.Should().Be(until);
        rig.Store.Cooldown.Should().Be(rig.Cooldown.Current);
        rig.Audio.Downloads.Should().HaveCount(3);

        var held = await rig.AdvanceAsync(steps: 1);

        held.Value.Should().Be(new AdvanceJobs.WaitUntil(until, NotBefore: TestTime.Start));
        rig.Audio.Downloads.Should().HaveCount(3);

        rig.Time.Advance(Cooldown.Base);
        await rig.AdvanceAsync(steps: 1);

        rig.Audio.Downloads[^1].Should().Be(Samples.Video("v3"));
    }

    [Fact]
    public async Task Ending_the_cooldown_resumes_the_jobs_and_keeps_its_strikes()
    {
        var rig = new JobRig(Samples.Release(TrackCount));
        rig.Audio.Script(null, null, new RemoteFailure.RateLimited("429"));
        await rig.Start.HandleAsync(new StartJob.Command(Samples.AlbumUrl), Cancellation);
        await rig.AdvanceAsync(steps: 4);

        await rig.EndCooldown.HandleAsync(new EndCooldown.Command(), Cancellation);
        await rig.AdvanceAsync(steps: 1);

        rig.Audio.Downloads[^1].Should().Be(Samples.Video("v3"));
        rig.Store.Cooldown.Should().Be(rig.Cooldown.Current);
        rig.Cooldown.Current.Strikes.Should().Be(1);
    }

    [Fact]
    public async Task A_failed_download_waits_for_its_retry_and_no_ring_ends_the_pause()
    {
        var rig = new JobRig(Samples.Release(TrackCount));
        rig.Audio.Script(Samples.Transient);
        await rig.Start.HandleAsync(new StartJob.Command(Samples.AlbumUrl), Cancellation);

        var failed = await rig.AdvanceAsync(steps: 2);

        var wait = failed.Value.Should().BeOfType<AdvanceJobs.WaitUntil>().Subject;
        var waiting = rig.Jobs.List(1)[0].State.Should().BeOfType<JobState.Waiting>().Subject;
        wait.At.Should().Be(waiting.RetryAt);
        wait.NotBefore.Should()
            .BeOnOrAfter(TestTime.Start + TimeSpan.FromSeconds(Pacing.MinPauseSeconds))
            .And.BeOnOrBefore(TestTime.Start + TimeSpan.FromSeconds(Pacing.MaxPauseSeconds));
    }

    [Fact]
    public async Task A_job_that_downloads_every_track_is_ready_and_clears_the_cooldown()
    {
        var rig = new JobRig(Samples.Release(TrackCount));
        var past = Cooldown.None.Hit("429", TestTime.Start - TimeSpan.FromDays(1));
        rig.Cooldown.Set(past);
        rig.Store.Cooldown = past;
        await rig.Start.HandleAsync(new StartJob.Command(Samples.AlbumUrl), Cancellation);

        var packaged = await rig.AdvanceAsync(steps: 1 + TrackCount + 1);

        rig.Jobs.List(1)[0]
            .State.Should()
            .Be(new JobState.Ready(FakeJobOutput.FileName, FakeJobOutput.FileSize, TestTime.Start));
        rig.Cooldown.Current.Should().Be(Cooldown.None);
        rig.Store.Cooldown.Should().Be(Cooldown.None);
        packaged.Value.Should().BeOfType<AdvanceJobs.WaitUntil>();
        (await rig.AdvanceAsync(steps: 1)).Value.Should().BeOfType<AdvanceJobs.Idle>();
    }

    [Fact]
    public async Task A_provider_without_the_release_still_gives_a_ready_job_that_keeps_the_failure()
    {
        var rig = new JobRig(Samples.Release(TrackCount));
        rig.Enricher.Result = new NotEnriched(EnrichmentFailure.Unreachable);
        await rig.Start.HandleAsync(new StartJob.Command(Samples.AlbumUrl), Cancellation);

        await rig.AdvanceAsync(steps: 1 + TrackCount + 1);

        var job = rig.Jobs.List(1)[0];
        job.State.Should()
            .Be(new JobState.Ready(FakeJobOutput.FileName, FakeJobOutput.FileSize, TestTime.Start));
        job.EnrichmentFailure.Should().Be(EnrichmentFailure.Unreachable);
        rig.Output.Packaged.Should().Equal((ReleaseDetails?)null);
        rig.Cooldown.Current.Should().Be(Cooldown.None);
    }

    [Fact]
    public async Task The_details_of_the_provider_reach_the_output_and_leave_no_failure()
    {
        var details = new ReleaseDetails(
            "Label",
            Barcode: null,
            ReleaseDate: null,
            ReleaseType: null,
            1,
            []
        );
        var rig = new JobRig(Samples.Release(TrackCount));
        rig.Enricher.Result = details;
        await rig.Start.HandleAsync(new StartJob.Command(Samples.AlbumUrl), Cancellation);

        await rig.AdvanceAsync(steps: 1 + TrackCount + 1);

        rig.Jobs.List(1)[0].EnrichmentFailure.Should().BeNull();
        rig.Output.Packaged.Should().Equal(details);
        rig.Enricher.Lookups.Should().ContainSingle();
    }

    [Fact]
    public async Task A_restart_during_a_cooldown_requeues_the_job_and_calls_nothing_until_it_ends()
    {
        var rig = new JobRig(Samples.Release(TrackCount));
        var interrupted = Samples.Downloading(Samples.Release(TrackCount)).CompleteTrack();
        var cooldown = Cooldown.None.Hit("429", TestTime.Start);
        rig.Store.Jobs[interrupted.Id] = interrupted;
        rig.Store.Cooldown = cooldown;

        await rig.Restore.HandleAsync(new RestoreJobs.Command(), Cancellation);
        var held = await rig.AdvanceAsync(steps: 1);

        var job = rig.Jobs.List(1)[0];
        job.State.Should().BeOfType<JobState.Queued>();
        job.Tracks.Select(track => track.State)
            .Should()
            .Equal(TrackState.Done, TrackState.Pending, TrackState.Pending, TrackState.Pending);
        held.Value.Should().BeOfType<AdvanceJobs.WaitUntil>().Which.At.Should().Be(cooldown.Until);
        rig.Catalog.Lookups.Should().Be(0);
        rig.Audio.Downloads.Should().BeEmpty();
    }

    [Fact]
    public async Task A_video_job_is_ready_after_one_download_without_a_catalog_call()
    {
        var rig = new JobRig(Samples.Release(TrackCount));
        var queued = Samples.QueuedVideo(Samples.VideoRequest(quality: VideoQuality.P2160));
        await rig.Jobs.AddAsync(queued, Cancellation);

        await rig.AdvanceAsync(steps: 1);

        var kept = rig.Jobs.Get(queued.Id)!;
        kept.KeptVideo.Should().Be(Samples.VideoDetails);
        kept.State.Should().BeOfType<JobState.Downloading>();

        await rig.AdvanceAsync(steps: 1);

        rig.Jobs.Get(queued.Id)!
            .State.Should()
            .Be(new JobState.Ready(FakeJobOutput.FileName, FakeJobOutput.FileSize, TestTime.Start));
        rig.Videos.Downloads.Should().Equal((Samples.Video("vid"), VideoQuality.P2160));
        rig.Catalog.Lookups.Should().Be(0);
        rig.Enricher.Lookups.Should().BeEmpty();
        rig.Audio.Downloads.Should().BeEmpty();
    }

    [Fact]
    public async Task A_restarted_video_job_whose_file_is_kept_is_not_downloaded_again()
    {
        var rig = new JobRig(Samples.Release(TrackCount));
        var interrupted = Samples.DownloadingVideo();
        rig.Store.Jobs[interrupted.Id] = interrupted;
        rig.Videos.Kept[interrupted.Id] = Samples.VideoDetails;

        await rig.Restore.HandleAsync(new RestoreJobs.Command(), Cancellation);
        await rig.AdvanceAsync(steps: 2);

        rig.Jobs.Get(interrupted.Id)!.State.Should().BeOfType<JobState.Ready>();
        rig.Videos.Downloads.Should().BeEmpty();
    }

    [Fact]
    public async Task A_playlist_link_whose_album_is_already_held_is_skipped_once_it_resolves()
    {
        const string playlistUrl = "https://music.youtube.com/playlist?list=OLAK5uy_x";
        var rig = new JobRig(Samples.Release(TrackCount));
        var first = await rig.Start.HandleAsync(
            new StartJob.Command(Samples.AlbumUrl),
            Cancellation
        );
        await rig.AdvanceAsync(steps: 1 + TrackCount + 1);

        var second = await rig.Start.HandleAsync(new StartJob.Command(playlistUrl), Cancellation);
        await rig.AdvanceAsync(steps: 1);

        var held = first.Value.Should().BeOfType<StartJob.Started>().Subject;
        var skipped = second.Value.Should().BeOfType<StartJob.Started>().Subject;
        rig.Jobs.Get(skipped.Id)!.State.Should().Be(new JobState.Skipped(held.Id, TestTime.Start));
        rig.Audio.Downloads.Should().HaveCount(TrackCount);
    }
}
