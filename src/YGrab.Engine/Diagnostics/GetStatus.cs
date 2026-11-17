// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine.Diagnostics;

public static class GetStatus
{
    public sealed record Query;

    /// <param name="Components">Versions of every adapter, in registration order.</param>
    public sealed record Status(ImmutableArray<ComponentVersion> Components, ServerUsage Usage);

    internal sealed class Handler(IEnumerable<IComponentVersions> versions, IServerUsage usage)
        : IQueryHandler<Query, Status>
    {
        public async ValueTask<Status> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var reads = versions.Select(source => source.ReadAsync(cancellationToken).AsTask());
            var components = await Task.WhenAll(reads);
            return new Status([.. components.SelectMany(list => list)], usage.Read());
        }
    }
}
