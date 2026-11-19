// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Time.Testing;

namespace YGrab.Tests.Fakes;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that reports its first timer.
/// </summary>
/// <remarks>
/// A test awaits <see cref="FirstTimer"/> before it advances the time. A timer created after the
/// advance would start from the new time and never fire.
/// </remarks>
internal sealed class TimerWatchingTimeProvider(DateTimeOffset start) : FakeTimeProvider(start)
{
    public TaskCompletionSource FirstTimer { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period
    )
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        FirstTimer.TrySetResult();
        return timer;
    }
}
