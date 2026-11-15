// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YGrab.Infrastructure.Configuration;
using YGrab.Infrastructure.Database;

namespace YGrab.Tests.Fakes;

/// <summary>
/// A database in a new temporary directory, deleted on dispose. The writer starts only on
/// <see cref="StartAsync"/>.
/// </summary>
internal sealed class TempDatabase : IAsyncDisposable
{
    private readonly TempDirectory _directory = new();

    public TempDatabase(TimeProvider timeProvider)
    {
        Database = new SqliteDatabase(
            Options.Create(new StorageOptions { DataDirectory = _directory.FullName })
        );
        Writer = new SqliteWriter(
            Database,
            new SchemaMigrator(timeProvider, NullLogger<SchemaMigrator>.Instance),
            NullLogger<SqliteWriter>.Instance
        );
    }

    public SqliteDatabase Database { get; }

    public SqliteWriter Writer { get; }

    public Task StartAsync(CancellationToken cancellationToken) =>
        Writer.StartAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await Writer.StopAsync(CancellationToken.None);
        Writer.Dispose();
        SqliteConnection.ClearAllPools();
        _directory.Dispose();
    }
}
