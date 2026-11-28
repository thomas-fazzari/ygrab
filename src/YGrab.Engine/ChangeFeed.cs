// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;

namespace YGrab.Engine;

/// <summary>
/// A change that clients see through the change feed.
/// </summary>
public closed record Change;

/// <summary>
/// Hands each change to every subscriber.
/// </summary>
/// <remarks>
/// There is no replay, and publishing never waits.
/// </remarks>
public sealed class ChangeFeed
{
    internal const int SUBSCRIBER_CAPACITY = 1024;

    private readonly ConcurrentDictionary<Subscription, byte> _subscribers = new();

    public void Publish(Change change)
    {
        ArgumentNullException.ThrowIfNull(change);

        foreach (var (subscriber, _) in _subscribers)
        {
            if (!subscriber.TryWrite(change))
                subscriber.Dispose();
        }
    }

    /// <summary>
    /// Sees every change published from this call until it is disposed.
    /// </summary>
    /// <remarks>
    /// The queue is bounded, and a full queue ends the subscription once the reader drains it.
    /// </remarks>
    public Subscription Subscribe()
    {
        var subscription = new Subscription(
            this,
            Channel.CreateBounded<Change>(
                new BoundedChannelOptions(SUBSCRIBER_CAPACITY)
                {
                    SingleReader = true,
                    FullMode = BoundedChannelFullMode.Wait,
                }
            )
        );
        _subscribers.TryAdd(subscription, 0);
        return subscription;
    }

    public sealed class Subscription : IDisposable
    {
        private readonly ChangeFeed _feed;
        private readonly Channel<Change> _channel;

        internal Subscription(ChangeFeed feed, Channel<Change> channel)
        {
            _feed = feed;
            _channel = channel;
        }

        /// <summary>
        /// Reads the changes, and completes once the subscription ended and its queue is empty.
        /// </summary>
        public ChannelReader<Change> Reader => _channel.Reader;

        public void Dispose()
        {
            _feed._subscribers.TryRemove(this, out _);
            _channel.Writer.TryComplete();
        }

        internal bool TryWrite(Change change) => _channel.Writer.TryWrite(change);
    }
}
