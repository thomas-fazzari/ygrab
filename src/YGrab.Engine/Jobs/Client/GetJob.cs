// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

public static class GetJob
{
    public sealed record GetJobRequest(JobId Id) : IQuery<Outcome>;

    public readonly union Outcome(Job, NotFound);

    internal sealed class Handler(JobRegistry jobs) : IRequestHandler<GetJobRequest, Outcome>
    {
        public ValueTask<Outcome> HandleAsync(
            GetJobRequest request,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(request);

            return ValueTask.FromResult<Outcome>(
                jobs.Get(request.Id) is { } job ? job : new NotFound()
            );
        }
    }
}
