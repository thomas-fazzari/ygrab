// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Logging.Abstractions;
using YGrab.Engine.Jobs;
using YGrab.Infrastructure.Jobs;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Infrastructure;

public sealed class JobRunsTests
{
    private static readonly TimeSpan _pause = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _retry = TimeSpan.FromMinutes(10);

    [Fact(Timeout = 5_000)]
    public async Task A_ring_ends_the_wait_for_a_retry_once_the_pause_is_over()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var time = new TimerWatchingTimeProvider(TestTime.Start);
        var signal = new JobSignal();
        var advance = new FakeAdvanceJobs(
            time,
            new AdvanceJobs.WaitUntil(TestTime.Start + _retry, NotBefore: TestTime.Start + _pause)
        );
        using var runs = new JobRuns(advance, signal, time, NullLogger<JobRuns>.Instance);
        signal.Ring();
        await runs.StartAsync(cancellation);
        await time.FirstTimer.Task.WaitAsync(cancellation);

        time.Advance(_pause);
        var woken = await advance.SecondCall.Task.WaitAsync(cancellation);

        woken.Should().Be(TestTime.Start + _pause);
        await runs.StopAsync(cancellation);
    }
}
