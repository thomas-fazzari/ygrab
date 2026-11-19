// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Infrastructure.Database;

/// <summary>
/// Runs every write on one connection, one transaction per work item.
/// </summary>
public sealed partial class SqliteWriter(
    SqliteDatabase database,
    SchemaMigrator migrator,
    ILogger<SqliteWriter> logger
) : BackgroundService
{
    private const int QueueCapacity = 1024;

    private readonly Channel<WorkItem> _queue = Channel.CreateBounded<WorkItem>(
        new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        }
    );

    private SqliteConnection? _connection;

    /// <summary>
    /// Runs <paramref name="work"/> in its own transaction.
    /// </summary>
    /// <remarks>
    /// The token stops a write only while it is queued. A write that has started commits even if
    /// the token is cancelled afterwards.
    /// </remarks>
    public async Task WriteAsync(Action<SqliteConnection> work, CancellationToken cancellationToken)
    {
        var item = new WorkItem(work, cancellationToken);
        await _queue.Writer.WriteAsync(item, cancellationToken);
        await item.Completion.Task;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var connection = database.OpenWriteConnection();
        try
        {
            migrator.Migrate(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        _connection = connection;
        await base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Writer.TryComplete();
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _connection?.Dispose();
        base.Dispose();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = _connection
            ?? throw new InvalidOperationException("The writer was not started.");
        _connection = null;

        // A commit waits for fsync, so the loop owns a thread instead of borrowing a pool thread
        return Task.Factory.StartNew(
            () => Run(connection),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    private void Run(SqliteConnection connection)
    {
        Exception? failure = null;

        try
        {
            var reader = _queue.Reader;
            while (reader.WaitToReadAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult())
            {
                while (reader.TryRead(out var item))
                {
                    Process(connection, item);
                }
            }
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            FailPending(failure);
            connection.Dispose();
        }
    }

    private void Process(SqliteConnection connection, WorkItem item)
    {
        if (item.TryCancel())
            return;

        SqliteTransaction? transaction = null;
        Exception? failure = null;
        try
        {
            transaction = connection.BeginTransaction(deferred: false);
            item.Run(connection, transaction);
        }
        catch (Exception exception)
        {
            LogWriteFailed(exception);
            failure = exception;
        }

        // Dispose rolls back unless the commit succeeded or SQLite already rolled back
        // A rollback that throws stops the writer
        try
        {
            transaction?.Dispose();
        }
        finally
        {
            if (failure is not null)
                item.Fail(failure);
        }
    }

    private void FailPending(Exception? failure)
    {
        _queue.Writer.TryComplete(failure);

        var stopped = new InvalidOperationException("The database writer stopped.", failure);
        while (_queue.Reader.TryRead(out var item))
        {
            item.Fail(stopped);
        }
    }

    private sealed class WorkItem(
        Action<SqliteConnection> work,
        CancellationToken cancellationToken
    )
    {
        public TaskCompletionSource Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool TryCancel()
        {
            if (!cancellationToken.IsCancellationRequested)
                return false;

            Completion.TrySetCanceled(cancellationToken);
            return true;
        }

        public void Run(SqliteConnection connection, SqliteTransaction transaction)
        {
            work(connection);
            transaction.Commit();
            Completion.TrySetResult();
        }

        public void Fail(Exception exception) => Completion.TrySetException(exception);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A database write failed and was rolled back")]
    private partial void LogWriteFailed(Exception exception);
}
