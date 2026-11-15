// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using YGrab.Contracts;
using YGrab.Contracts.Jobs;
using YGrab.Domain;
using YGrab.Engine;
using YGrab.Engine.Jobs;
using YGrab.Host.Jobs;

namespace YGrab.Host.Events;

internal static class EventsEndpoints
{
    internal static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    // An SSE comment line, which clients ignore and proxies count as traffic
    private static readonly byte[] _heartbeat = ":\n\n"u8.ToArray();

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapEvents()
        {
            endpoints.MapGet("/events", Stream);
            return endpoints;
        }
    }

    private static ServerSentEventsResult<string> Stream(
        ChangeFeed feed,
        IQueryHandler<ListJobs.Query, ListJobs.Outcome> listJobs,
        IQueryHandler<GetCooldown.Query, Cooldown> getCooldown,
        IHostApplicationLifetime lifetime,
        TimeProvider timeProvider,
        HttpResponse response,
        CancellationToken cancellationToken
    ) =>
        TypedResults.ServerSentEvents(
            RelayAsync(
                feed,
                listJobs,
                getCooldown,
                lifetime,
                timeProvider,
                response,
                cancellationToken
            )
        );

    // Subscribes before the headers flush and the snapshot read, so a client misses no change
    // and sees one twice at worst
    private static async IAsyncEnumerable<SseItem<string>> RelayAsync(
        ChangeFeed feed,
        IQueryHandler<ListJobs.Query, ListJobs.Outcome> listJobs,
        IQueryHandler<GetCooldown.Query, Cooldown> getCooldown,
        IHostApplicationLifetime lifetime,
        TimeProvider timeProvider,
        HttpResponse response,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        using var subscription = feed.Subscribe();
        // Ending the subscription ends the stream, so the host does not wait for the client
        await using var stop = lifetime.ApplicationStopping.Register(subscription.Dispose);
        await response.StartAsync(cancellationToken);
        await response.Body.FlushAsync(cancellationToken);

        var jobs = (
            await listJobs.HandleAsync(new ListJobs.Query(ListJobs.MaxLimit), cancellationToken)
        ).Value switch
        {
            ListJobs.Listed listed => listed.Jobs,
            Failure failure => throw new InvalidOperationException(
                $"The snapshot limit is invalid: {failure}"
            ),
            _ => throw Unreachable.NullUnion(),
        };
        var cooldown = await getCooldown.HandleAsync(new GetCooldown.Query(), cancellationToken);
        yield return new SseItem<string>(
            JsonSerializer.Serialize(
                new SnapshotDto(
                    ImmutableArray.CreateRange(jobs, job => job.ToDto()),
                    cooldown.ToDto()
                ),
                ApiJsonContext.Default.SnapshotDto
            ),
            EventNames.Snapshot
        );

        using var timer = new PeriodicTimer(HeartbeatInterval, timeProvider);
        var changedSinceTick = false;
        var changes = subscription.WaitToReadAsync(cancellationToken).AsTask();
        var ticks = timer.WaitForNextTickAsync(cancellationToken).AsTask();
        while (true)
        {
            if (await Task.WhenAny(changes, ticks) == ticks)
            {
                if (!await ticks)
                {
                    yield break;
                }

                if (!changedSinceTick)
                {
                    await response.Body.WriteAsync(_heartbeat, cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                }

                changedSinceTick = false;
                ticks = timer.WaitForNextTickAsync(cancellationToken).AsTask();
                continue;
            }

            if (!await changes)
            {
                yield break;
            }

            while (subscription.TryRead(out var change))
            {
                changedSinceTick = true;
                yield return ToItem(change);
            }

            changes = subscription.WaitToReadAsync(cancellationToken).AsTask();
        }
    }

    private static SseItem<string> ToItem(Change change) =>
        change switch
        {
            JobChanged changed => new(
                JsonSerializer.Serialize(changed.Job.ToDto(), ApiJsonContext.Default.JobDto),
                EventNames.Job
            ),
            JobRemoved removed => new(
                JsonSerializer.Serialize(
                    new JobRemovedDto(removed.Id),
                    ApiJsonContext.Default.JobRemovedDto
                ),
                EventNames.JobRemoved
            ),
            CooldownChanged changed => new(
                JsonSerializer.Serialize(
                    changed.Cooldown.ToDto(),
                    ApiJsonContext.Default.CooldownDto
                ),
                EventNames.Cooldown
            ),
        };
}
