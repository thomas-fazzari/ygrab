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
) : BackgroundLoop
{
    internal static readonly TimeSpan FailureDelay = TimeSpan.FromMinutes(1);

    protected override async Task RunAsync(CancellationToken stoppingToken)
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
                await WaitAsync(wait, cancellationToken);
                break;
            case AdvanceJobs.Stepped:
                break;
            default:
                throw Unreachable.NullUnion();
        }
    }

    // A ring after the pause ends the wait, in case a client removed the waiting job
    private async Task WaitAsync(AdvanceJobs.WaitUntil wait, CancellationToken cancellationToken)
    {
        await Task.Delay(Remaining(wait.NotBefore), timeProvider, cancellationToken);
        var rest = Remaining(wait.At);
        if (rest == TimeSpan.Zero)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(rest, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token
        );
        try
        {
            await signal.WaitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
    }

    private TimeSpan Remaining(DateTimeOffset at)
    {
        var remaining = at - timeProvider.GetUtcNow();
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Advancing the jobs failed")]
    private static partial void LogStepFailed(ILogger logger, Exception exception);
}
