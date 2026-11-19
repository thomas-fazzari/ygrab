// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

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
    public sealed record Command(Guid Id);

    public sealed record Removed;

    public readonly union Outcome(Removed, NotFound);

    internal sealed class Handler(
        JobRegistry jobs,
        ActiveStep activeStep,
        IJobStore store,
        IJobOutput output,
        JobSignal signal
    ) : ICommandHandler<Command, Outcome>
    {
        public async ValueTask<Outcome> HandleAsync(
            Command command,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(command);

            if (jobs.Remove(command.Id) is null)
                return new NotFound();

            signal.Ring();
            await activeStep.CancelAsync(command.Id);
            await store.DeleteAsync(command.Id, CancellationToken.None);
            output.Delete(command.Id);
            return new Removed();
        }
    }
}
