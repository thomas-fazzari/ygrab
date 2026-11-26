// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine;
using YGrab.Engine.Jobs;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Engine;

public sealed class MoveJobTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_move_to_the_top_keeps_the_active_job_first_once_it_queues_again()
    {
        var release = Samples.Release(2);
        var rig = new JobRig(release);
        var active = Samples.Downloading(release);
        var first = Samples.Queued(Samples.AlbumLink());
        var second = Samples.Queued(Samples.AlbumLink());
        var third = Samples.Queued(Samples.AlbumLink());
        rig.Jobs.Load([active, first, second, third]);
        rig.Store.Jobs[third.Id] = third;

        var top = await MoveAsync(rig, third.Id, new MoveJob.Destination.Before(active.Id));
        var last = await MoveAsync(rig, first.Id, new MoveJob.Destination.Last());
        var running = await MoveAsync(rig, active.Id, new MoveJob.Destination.Before(second.Id));

        top.Value.Should().BeOfType<MoveJob.Moved>();
        last.Value.Should().BeOfType<MoveJob.Moved>();
        running.Value.Should().BeOfType<NotMovable>();
        rig.Jobs.Queued().Select(job => job.Id).Should().Equal(third.Id, second.Id, first.Id);
        rig.Store.Jobs[third.Id].QueueRank.Should().Be(rig.Jobs.Get(third.Id)!.QueueRank);

        rig.Jobs.Update(active.Id, job => job.Requeue());

        rig.Jobs.NextQueued()!.Id.Should().Be(active.Id);
    }

    [Fact]
    public async Task A_move_to_the_top_ranks_below_a_failed_job_that_queues_again()
    {
        var release = Samples.Release(2);
        var rig = new JobRig(release);
        var failed = Samples.Downloading(release).Abort("broken", TestTime.Start);
        var first = Samples.Queued(Samples.AlbumLink());
        var second = Samples.Queued(Samples.AlbumLink(), now: TestTime.Start.AddMinutes(1));
        rig.Jobs.Load([failed, first, second]);

        await MoveAsync(rig, second.Id, new MoveJob.Destination.Before(first.Id));
        rig.Jobs.Update(failed.Id, job => job.Retry(new HashSet<VideoId>()));

        rig.Jobs.Queued().Select(job => job.Id).Should().Equal(second.Id, failed.Id, first.Id);
    }

    private static ValueTask<MoveJob.Outcome> MoveAsync(
        JobRig rig,
        JobId id,
        MoveJob.Destination to
    ) => rig.Move.HandleAsync(new MoveJob.MoveJobRequest(id, to), Cancellation);
}
