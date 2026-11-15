// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Engine;
using YGrab.Engine.Jobs;

namespace YGrab.Infrastructure.Jobs;

/// <summary>
/// Advances the jobs one step at a time, so YouTube sees one call at a time.
/// </summary>
/// <remarks>
/// This loop owns every wait. <see cref="AdvanceJobs"/> only says how long to wait.
/// </remarks>
internal sealed partial class JobRuns(
    ICommandHandler<AdvanceJobs.Command, AdvanceJobs.Outcome> advanceJobs,
    JobSignal signal,
    TimeProvider timeProvider,
    ILogger<JobRuns> logger
) : BackgroundService
{
    internal static readonly TimeSpan FailureDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StepAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogStepFailed(logger, exception);
                await Task.Delay(FailureDelay, timeProvider, stoppingToken);
            }
        }
    }

    private async Task StepAsync(CancellationToken cancellationToken)
    {
        switch (await advanceJobs.HandleAsync(new AdvanceJobs.Command(), cancellationToken))
        {
            case AdvanceJobs.Idle:
                await signal.WaitAsync(cancellationToken);
                break;
            case AdvanceJobs.WaitUntil wait:
                var delay = wait.At - timeProvider.GetUtcNow();
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, timeProvider, cancellationToken);
                }

                break;
            case AdvanceJobs.Stepped:
                break;
            default:
                throw Unreachable.NullUnion();
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Advancing the jobs failed")]
    private static partial void LogStepFailed(ILogger logger, Exception exception);
}
