// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

public static class ListJobs
{
    public const int MaxLimit = 1000;

    private const string LimitMessage = "The limit is not between 1 and the maximum.";

    public sealed record Query(int Limit);

    public sealed record Listed(ImmutableArray<Job> Jobs);

    public readonly union Outcome(Listed, Invalid);

    internal sealed class Handler(JobRegistry jobs) : IQueryHandler<Query, Outcome>
    {
        public ValueTask<Outcome> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            return ValueTask.FromResult<Outcome>(
                query.Limit is < 1 or > MaxLimit
                    ? new Invalid(nameof(Query.Limit), LimitMessage)
                    : new Listed(jobs.List(query.Limit))
            );
        }
    }
}
