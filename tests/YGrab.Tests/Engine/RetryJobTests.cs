// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine;
using YGrab.Engine.Jobs;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Engine;

public sealed class RetryJobTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_failed_job_queues_again_once_and_a_link_that_another_job_covers_is_refused()
    {
        var release = Samples.Release(2);
        var rig = new JobRig(release);
        var first = Samples.Downloading(release).Abort("broken", TestTime.Start);
        var second = Samples.Downloading(release).Abort("broken", TestTime.Start);
        rig.Jobs.Load([first, second]);
        rig.Store.Jobs[first.Id] = first;

        var retried = await rig.Retry.HandleAsync(
            new RetryJob.RetryJobRequest(first.Id),
            Cancellation
        );
        var again = await rig.Retry.HandleAsync(
            new RetryJob.RetryJobRequest(first.Id),
            Cancellation
        );
        var refused = await rig.Retry.HandleAsync(
            new RetryJob.RetryJobRequest(second.Id),
            Cancellation
        );

        retried.Value.Should().BeOfType<RetryJob.Retried>();
        again.Value.Should().BeOfType<NotRetryable>();
        refused.Value.Should().BeOfType<AlreadyGrabbed>().Which.Previous.Id.Should().Be(first.Id);
        rig.Jobs.Get(first.Id)!.State.Should().BeOfType<JobState.Queued>();
        rig.Store.Jobs[first.Id].State.Should().BeOfType<JobState.Queued>();
        rig.Jobs.Get(second.Id)!.State.Should().BeOfType<JobState.Failed>();
    }
}
