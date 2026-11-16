// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Infrastructure.Jobs;

/// <summary>
/// A background service that ends quietly when the host cancels it.
/// </summary>
/// <remarks>
/// A host that fails to start cancels its services without a stop, and logs the cancellation of a
/// plain <see cref="BackgroundService"/> as a crash.
/// </remarks>
internal abstract class BackgroundLoop : BackgroundService
{
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    protected abstract Task RunAsync(CancellationToken stoppingToken);
}
