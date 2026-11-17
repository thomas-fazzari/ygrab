// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO.Enumeration;
using YGrab.Engine.Diagnostics;
using YGrab.Infrastructure.Configuration;

namespace YGrab.Infrastructure.Diagnostics;

/// <summary>
/// Reads the start time of the process, the size of the data folder and the free space of its disk.
/// </summary>
/// <remarks>
/// <para>
/// A running job creates, renames and deletes files during the walk. The walk skips a file that
/// is gone, so the size is close and the read never fails.
/// </para>
/// </remarks>
internal sealed class ServerUsageProbe(IOptions<StorageOptions> options) : IServerUsage
{
    private static readonly EnumerationOptions _everyFile = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    private readonly DateTimeOffset _startedAt = StartTime();

    public ServerUsage Read()
    {
        var directory = options.Value.DataDirectory;
        return new ServerUsage(
            _startedAt,
            Size(directory),
            new DriveInfo(directory).AvailableFreeSpace
        );
    }

    private static DateTimeOffset StartTime()
    {
        using var process = Process.GetCurrentProcess();
        return new DateTimeOffset(process.StartTime.ToUniversalTime());
    }

    private static long Size(string directory)
    {
        var lengths = new FileSystemEnumerable<long>(
            directory,
            (ref entry) => entry.Length,
            _everyFile
        )
        {
            ShouldIncludePredicate = (ref entry) => !entry.IsDirectory,
        };
        long total = 0;
        try
        {
            foreach (var length in lengths)
            {
                total += length;
            }
        }
        catch (IOException)
        {
            // A folder that a job deletes during the walk ends it with the size so far
        }

        return total;
    }
}
