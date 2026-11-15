// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Infrastructure.Configuration;

namespace YGrab.Infrastructure.Database;

public sealed class SqliteDatabase(IOptions<StorageOptions> options)
{
    internal const string DatabaseFileName = "ygrab.db";

    // The database and the job files are private to the user
    private const UnixFileMode DirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private readonly string _directory = options.Value.DataDirectory;
    private readonly string _path = Path.Combine(options.Value.DataDirectory, DatabaseFileName);

    /// <summary>
    /// Creates the data directory and the database file when they are missing.
    /// </summary>
    public SqliteConnection OpenWriteConnection()
    {
        CreateDirectory();
        return Open(SqliteOpenMode.ReadWriteCreate);
    }

    public SqliteConnection OpenReadConnection() => Open(SqliteOpenMode.ReadOnly);

    private void CreateDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(_directory);
            return;
        }

        Directory.CreateDirectory(_directory, DirectoryMode);
    }

    private SqliteConnection Open(SqliteOpenMode mode)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = mode,
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        try
        {
            connection.Open();
            if (mode is SqliteOpenMode.ReadWriteCreate)
            {
                // fullfsync makes macOS flush to the disk and SQLite ignores it on other systems
                connection.Execute("PRAGMA synchronous = FULL");
                connection.Execute("PRAGMA fullfsync = ON");
            }

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
