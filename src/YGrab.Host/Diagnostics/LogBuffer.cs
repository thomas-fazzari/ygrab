// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Channels;
using YGrab.Contracts.Diagnostics;

namespace YGrab.Host.Diagnostics;

/// <summary>
/// Keeps the last log lines in memory and hands each new line to every subscriber.
/// </summary>
/// <remarks>
/// <para>
/// It keeps <see cref="Capacity"/> lines and cuts a message after <see cref="MaxMessageLength"/>
/// characters.
/// </para>
/// <para>
/// A subscriber gets the kept lines, then every later line, with no gap and no repeat. A full
/// subscriber queue ends the subscription once the reader drains it.
/// </para>
/// </remarks>
[ProviderAlias(Alias)]
internal sealed class LogBuffer(TimeProvider timeProvider) : ILoggerProvider
{
    public const string Alias = "Diagnostics";
    internal const int Capacity = 1000;
    internal const int MaxMessageLength = 16 * 1024;
    internal const int SubscriberCapacity = 1024;

    // Logger of each use case is its nested Handler type, a name that tells nothing
    private const string HandlerTypeName = "Handler";

    private static readonly BoundedChannelOptions _subscriberOptions = new(SubscriberCapacity)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait,
    };

    // Guards the lines and the subscribers together, so a new subscriber misses no line
    private readonly Lock _lock = new();
    private readonly Queue<LogEntryDto> _entries = new(Capacity);
    private ImmutableArray<Subscription> _subscribers = [];

    public ILogger CreateLogger(string categoryName) => new Logger(this, SourceOf(categoryName));

    public Subscription Subscribe()
    {
        lock (_lock)
        {
            var subscription = new Subscription(this, [.. _entries]);
            _subscribers = _subscribers.Add(subscription);
            return subscription;
        }
    }

    public void Dispose()
    {
        foreach (var subscriber in _subscribers)
        {
            subscriber.Dispose();
        }
    }

    private void Add(LogLevel level, string source, string message)
    {
        var entry = new LogEntryDto(timeProvider.GetUtcNow(), CodeOf(level), source, message);
        lock (_lock)
        {
            if (_entries.Count == Capacity)
                _entries.Dequeue();

            _entries.Enqueue(entry);
            foreach (var subscriber in _subscribers)
            {
                subscriber.Write(entry);
            }
        }
    }

    private void Remove(Subscription subscription)
    {
        lock (_lock)
        {
            _subscribers = _subscribers.Remove(subscription);
        }
    }

    private static string SourceOf(string category)
    {
        var segments = category.Split('.');
        return segments is [.., var owner, HandlerTypeName] ? owner : segments[^1];
    }

    private static LogLevelCode CodeOf(LogLevel level) =>
        level switch
        {
            LogLevel.Trace => LogLevelCode.Trace,
            LogLevel.Debug => LogLevelCode.Debug,
            LogLevel.Information => LogLevelCode.Information,
            LogLevel.Warning => LogLevelCode.Warning,
            LogLevel.Error => LogLevelCode.Error,
            LogLevel.Critical => LogLevelCode.Critical,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, message: null),
        };

    public sealed class Subscription(LogBuffer buffer, ImmutableArray<LogEntryDto> backlog)
        : IDisposable
    {
        private readonly Channel<LogEntryDto> _channel = Channel.CreateBounded<LogEntryDto>(
            _subscriberOptions
        );

        /// <summary>
        /// The lines kept when the subscription started, oldest first.
        /// </summary>
        public ImmutableArray<LogEntryDto> Backlog => backlog;

        /// <summary>
        /// Reads the later lines, and completes once the subscription ended and its queue is empty.
        /// </summary>
        public ChannelReader<LogEntryDto> Reader => _channel.Reader;

        public void Dispose()
        {
            buffer.Remove(this);
            _channel.Writer.TryComplete();
        }

        internal void Write(LogEntryDto entry)
        {
            if (!_channel.Writer.TryWrite(entry))
                _channel.Writer.TryComplete();
        }
    }

    private sealed class Logger(LogBuffer buffer, string source) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (exception is not null)
                message = $"{message}\n{exception}";

            buffer.Add(logLevel, source, Truncate(message));
        }

        private static string Truncate(string message) =>
            message.Length > MaxMessageLength ? message[..MaxMessageLength] : message;
    }
}
