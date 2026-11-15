// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Engine;

public sealed class ExpireJobsTests
{
    [Fact]
    public async Task A_sweep_deletes_the_folder_of_an_old_failed_job_and_goes_on_when_a_delete_throws()
    {
        var retention = TimeSpan.FromDays(7);
        var release = Samples.Release(2);
        var rig = new JobRig(release);
        var stuck = Samples.Ready(release);
        var ready = Samples.Ready(release);
        var failed = Samples.Downloading(release).Abort("broken", TestTime.Start);
        rig.Jobs.Load([stuck, ready, failed]);
        rig.Output.FailingDeletes.Add(stuck.Id);
        rig.Time.Advance(retention + TimeSpan.FromMinutes(1));

        await rig.Expire.HandleAsync(
            new ExpireJobs.Command(retention, retention),
            TestContext.Current.CancellationToken
        );

        rig.Jobs.Get(stuck.Id)!.State.Should().BeOfType<JobState.Ready>();
        rig.Jobs.Get(ready.Id)!.State.Should().BeOfType<JobState.Expired>();
        rig.Jobs.Get(failed.Id)!.State.Should().BeOfType<JobState.Failed>();
        rig.Output.Deleted.Should().BeEquivalentTo([ready.Id, failed.Id]);
    }

    [Fact]
    public async Task A_video_job_expires_after_its_own_retention()
    {
        var videoRetention = TimeSpan.FromDays(2);
        var retention = TimeSpan.FromDays(7);
        var release = Samples.Release(2);
        var rig = new JobRig(release);
        var audio = Samples.Ready(release);
        var video = Samples
            .DownloadingVideo()
            .CompleteVideo(Samples.VideoDetails)
            .StartPackaging(TestTime.Start)
            .Finish("video.mp4", enrichmentFailure: null, TestTime.Start);
        rig.Jobs.Load([audio, video]);
        rig.Time.Advance(videoRetention + TimeSpan.FromMinutes(1));

        await rig.Expire.HandleAsync(
            new ExpireJobs.Command(retention, videoRetention),
            TestContext.Current.CancellationToken
        );

        rig.Jobs.Get(video.Id)!.State.Should().BeOfType<JobState.Expired>();
        rig.Jobs.Get(audio.Id)!.State.Should().BeOfType<JobState.Ready>();
        rig.Output.Deleted.Should().Equal(video.Id);
    }
}
