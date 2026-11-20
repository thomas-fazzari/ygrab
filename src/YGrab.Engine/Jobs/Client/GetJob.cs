// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

public static class GetJob
{
    public sealed record Query(JobId Id);

    public readonly union Outcome(Job, NotFound);

    internal sealed class Handler(JobRegistry jobs) : IQueryHandler<Query, Outcome>
    {
        public ValueTask<Outcome> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            return ValueTask.FromResult<Outcome>(
                jobs.Get(query.Id) is { } job ? job : new NotFound()
            );
        }
    }
}
