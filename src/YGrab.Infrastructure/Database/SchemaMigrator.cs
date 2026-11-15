// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Infrastructure.Database;

public sealed partial class SchemaMigrator(
    TimeProvider timeProvider,
    ILogger<SchemaMigrator> logger
)
{
    private const string ResourcePrefix = "migrations/";
    private const string ResourceSuffix = ".sql";

    private const string CreateVersionTableSql = """
        CREATE TABLE IF NOT EXISTS schema_version (
            version    INTEGER PRIMARY KEY,
            name       TEXT    NOT NULL,
            applied_at INTEGER NOT NULL
        ) STRICT
        """;

    private const string InsertVersionSql = """
        INSERT INTO schema_version (version, name, applied_at)
        VALUES (@Version, @Name, @AppliedAt)
        """;

    public void Migrate(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        // WAL persists in the file, and SQLite refuses to change the mode inside a transaction
        connection.ExecuteScalar<string>("PRAGMA journal_mode = WAL");
        connection.Execute(CreateVersionTableSql);

        var migrations = LoadMigrations();
        var applied = connection.Query<long>("SELECT version FROM schema_version").ToHashSet();

        var newest = migrations[^1].Version;
        if (applied.Count > 0 && applied.Max() > newest)
        {
            LogDatabaseIsNewer(applied.Max(), newest);
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The database has schema version {applied.Max()}, newer than the {newest} that this build knows."
                )
            );
        }

        foreach (
            var migration in migrations.Where(migration => !applied.Contains(migration.Version))
        )
        {
            Apply(connection, migration);
        }
    }

    private void Apply(SqliteConnection connection, Migration migration)
    {
        using var transaction = connection.BeginTransaction(deferred: false);
        connection.Execute(migration.Sql, transaction: transaction);
        connection.Execute(
            InsertVersionSql,
            new
            {
                migration.Version,
                migration.Name,
                AppliedAt = timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
            },
            transaction
        );
        transaction.Commit();
        LogMigrationApplied(migration.Version, migration.Name);
    }

    private static List<Migration> LoadMigrations()
    {
        var assembly = typeof(SchemaMigrator).Assembly;
        var migrations = new List<Migration>();

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (
                !resource.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                || !resource.EndsWith(ResourceSuffix, StringComparison.Ordinal)
            )
            {
                continue;
            }

            var name = resource[ResourcePrefix.Length..];
            var prefix = name.Split('_', 2)[0];
            if (
                !long.TryParse(
                    prefix,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var version
                )
            )
            {
                throw new InvalidOperationException($"Migration '{name}' has no numeric prefix.");
            }

            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
            migrations.Add(new Migration(version, name, reader.ReadToEnd()));
        }

        migrations.Sort((left, right) => left.Version.CompareTo(right.Version));
        for (var index = 1; index < migrations.Count; index++)
        {
            if (migrations[index].Version == migrations[index - 1].Version)
            {
                throw new InvalidOperationException(
                    $"Migrations '{migrations[index - 1].Name}' and '{migrations[index].Name}' share a version."
                );
            }
        }

        return migrations;
    }

    private sealed record Migration(long Version, string Name, string Sql);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Applied database migration {Version} {Name}"
    )]
    private partial void LogMigrationApplied(long version, string name);

    [LoggerMessage(
        Level = LogLevel.Critical,
        Message = "The database has schema version {Found}, newer than the {Newest} of this build"
    )]
    private partial void LogDatabaseIsNewer(long found, long newest);
}
