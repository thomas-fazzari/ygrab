// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Cancels a job and deletes it, with its row and its files.
/// </summary>
/// <remarks>
/// <para>
/// The job leaves the registry first, so no later update brings it back. The step that runs for
/// the job stops, then the row and the folder go.
/// </para>
/// <para>
/// The row is deleted with no token, since the writer drops a queued item whose token is
/// cancelled.
/// </para>
/// </remarks>
public static class RemoveJob
{
    public sealed record RemoveJobRequest(JobId Id) : ICommand<Outcome>;

    public sealed record Removed;

    public readonly union Outcome(Removed, NotFound);

    internal sealed class Handler(
        JobRegistry jobs,
        ActiveStep activeStep,
        IJobStore store,
        IJobOutput output,
        JobSignal signal
    ) : IRequestHandler<RemoveJobRequest, Outcome>
    {
        public async ValueTask<Outcome> HandleAsync(
            RemoveJobRequest request,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(request);

            if (jobs.Remove(request.Id) is null)
                return new NotFound();

            signal.Ring();
            await activeStep.CancelAsync(request.Id);
            await store.DeleteAsync(request.Id, CancellationToken.None);
            output.Delete(request.Id);
            return new Removed();
        }
    }
}
