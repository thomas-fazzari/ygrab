// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace YGrab.Host.Events;

/// <summary>
/// Relays the items of a subscription as server-sent events, with a heartbeat while it is quiet.
/// </summary>
internal static class EventStream
{
    internal static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    // An SSE comment line, which clients ignore and proxies count as traffic
    private static readonly byte[] _heartbeat = ":\n\n"u8.ToArray();

    /// <summary>
    /// Sends the headers at once, so the client sees the stream open before the first event.
    /// </summary>
    public static async ValueTask OpenAsync(
        HttpResponse response,
        CancellationToken cancellationToken
    )
    {
        await response.StartAsync(cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Yields one event per item until the reader completes or the client leaves.
    /// </summary>
    public static async IAsyncEnumerable<SseItem<string>> FollowAsync<T>(
        ChannelReader<T> reader,
        Func<T, SseItem<string>> toEvent,
        HttpResponse response,
        TimeProvider timeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        using var timer = new PeriodicTimer(HeartbeatInterval, timeProvider);
        var changedSinceTick = false;
        var items = reader.WaitToReadAsync(cancellationToken).AsTask();
        var ticks = timer.WaitForNextTickAsync(cancellationToken).AsTask();
        while (true)
        {
            if (await Task.WhenAny(items, ticks) == ticks)
            {
                if (!await ticks)
                    yield break;

                if (!changedSinceTick)
                {
                    await response.Body.WriteAsync(_heartbeat, cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                }

                changedSinceTick = false;
                ticks = timer.WaitForNextTickAsync(cancellationToken).AsTask();
                continue;
            }

            if (!await items)
                yield break;

            while (reader.TryRead(out var item))
            {
                changedSinceTick = true;
                yield return toEvent(item);
            }

            items = reader.WaitToReadAsync(cancellationToken).AsTask();
        }
    }
}
