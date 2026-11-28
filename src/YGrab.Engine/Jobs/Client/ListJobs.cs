// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

public static class ListJobs
{
    public const int MAX_LIMIT = 1000;

    private const string LIMIT_MESSAGE = "The limit is not between 1 and the maximum.";

    public sealed record ListJobsRequest(int Limit) : IQuery<Outcome>;

    public sealed record Listed(ImmutableArray<Job> Jobs);

    public readonly union Outcome(Listed, Invalid);

    internal sealed class Handler(JobRegistry jobs) : IRequestHandler<ListJobsRequest, Outcome>
    {
        public ValueTask<Outcome> HandleAsync(
            ListJobsRequest request,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(request);

            return ValueTask.FromResult<Outcome>(
                request.Limit is < 1 or > MAX_LIMIT
                    ? new Invalid(nameof(ListJobsRequest.Limit), LIMIT_MESSAGE)
                    : new Listed(jobs.List(request.Limit))
            );
        }
    }
}
