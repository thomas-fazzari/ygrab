// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Engine;
using YGrab.Engine.Jobs;

namespace YGrab.Infrastructure.Jobs;

/// <summary>
/// Expires the finished jobs that are older than the retention, at start and then at intervals.
/// </summary>
internal sealed partial class JobSweeper(
    ICommandHandler<ExpireJobs.Command, Done> expireJobs,
    IRetentionSource retention,
    TimeProvider timeProvider,
    ILogger<JobSweeper> logger
) : BackgroundLoop
{
    internal static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(10);

    protected override async Task RunAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval, timeProvider);
        do
        {
            await SweepAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            await expireJobs.HandleAsync(
                new ExpireJobs.Command(retention.Current),
                cancellationToken
            );
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogSweepFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Expiring the old jobs failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);
}
