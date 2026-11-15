// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine.Jobs;

/// <summary>
/// Wakes the job loop when there may be work.
/// </summary>
/// <remarks>
/// Rings that arrive before the loop waits again merge into one.
/// </remarks>
public sealed class JobSignal
{
    private readonly Channel<byte> _bell = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        }
    );

    public async ValueTask WaitAsync(CancellationToken cancellationToken) =>
        await _bell.Reader.ReadAsync(cancellationToken);

    internal void Ring() => _bell.Writer.TryWrite(0);
}
