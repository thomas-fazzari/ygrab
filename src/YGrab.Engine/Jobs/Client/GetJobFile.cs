// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <param name="Name">The file name that the browser saves.</param>
public sealed record JobFile(string Path, string Name);

public static class GetJobFile
{
    public sealed record Query(Guid Id);

    public readonly union Outcome(JobFile, NotFound, NotReady, Expired);

    internal sealed class Handler(JobRegistry jobs, IJobOutput output)
        : IQueryHandler<Query, Outcome>
    {
        public ValueTask<Outcome> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            return ValueTask.FromResult(Find(query.Id));
        }

        private Outcome Find(Guid id) =>
            jobs.Get(id) switch
            {
                null => new NotFound(),
                { State: JobState.Expired } => new Expired(),
                { State: JobState.Failed or JobState.Skipped } => new NotFound(),
                { State: JobState.Ready { FileName: var name } } => File(id, name),
                { State: JobState.Incomplete { FileName: var name } } => File(id, name),
                _ => new NotReady(),
            };

        private Outcome File(Guid id, string name) =>
            output.FindFile(id, name) is { } path ? new JobFile(path, name) : new Expired();
    }
}
