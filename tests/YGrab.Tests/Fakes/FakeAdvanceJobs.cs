// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Engine;
using YGrab.Engine.Jobs;

namespace YGrab.Tests.Fakes;

/// <summary>
/// Answers the first call with a given outcome and every later call with <see cref="AdvanceJobs.Idle"/>.
/// </summary>
internal sealed class FakeAdvanceJobs(TimeProvider time, AdvanceJobs.Outcome first)
    : ICommandHandler<AdvanceJobs.Command, AdvanceJobs.Outcome>
{
    private int _calls;

    public TaskCompletionSource<DateTimeOffset> SecondCall { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ValueTask<AdvanceJobs.Outcome> HandleAsync(
        AdvanceJobs.Command command,
        CancellationToken cancellationToken
    )
    {
        var call = Interlocked.Increment(ref _calls);
        if (call == 1)
        {
            return ValueTask.FromResult(first);
        }

        SecondCall.TrySetResult(time.GetUtcNow());
        return ValueTask.FromResult<AdvanceJobs.Outcome>(new AdvanceJobs.Idle());
    }
}
