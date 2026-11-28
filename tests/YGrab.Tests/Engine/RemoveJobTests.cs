// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Engine;

public sealed class RemoveJobTests
{
    private const int TRACK_COUNT = 2;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_job_removed_during_its_download_leaves_no_row_and_no_folder_and_the_next_step_waits_a_pause()
    {
        var rig = new JobRig(Samples.Release(TRACK_COUNT));
        var started = await rig.Start.HandleAsync(
            new StartJob.StartJobRequest(Samples.ALBUM_URL),
            Cancellation
        );
        var id = started.Value.Should().BeOfType<StartJob.Started>().Subject.Id;
        await rig.AdvanceAsync(steps: 1);
        rig.Audio.WaitAfterNextDownload();
        var step = rig
            .Advance.HandleAsync(new AdvanceJobs.AdvanceJobsRequest(), Cancellation)
            .AsTask();
        await rig.Audio.Waiting;

        var removed = await rig.Remove.HandleAsync(
            new RemoveJob.RemoveJobRequest(id),
            Cancellation
        );
        rig.Audio.Release();
        var outcome = await step;

        removed.Value.Should().BeOfType<RemoveJob.Removed>();
        rig.Jobs.Get(id).Should().BeNull();
        rig.Store.Jobs.Should().NotContainKey(id);
        rig.Output.Deleted.Should().Equal(id, id);
        var pause = outcome.Value.Should().BeOfType<AdvanceJobs.WaitUntil>().Subject;
        pause
            .At.Should()
            .BeOnOrAfter(TestTime.Start + TimeSpan.FromSeconds(Pacing.MIN_PAUSE_SECONDS))
            .And.BeOnOrBefore(TestTime.Start + TimeSpan.FromSeconds(Pacing.MAX_PAUSE_SECONDS));
    }
}
