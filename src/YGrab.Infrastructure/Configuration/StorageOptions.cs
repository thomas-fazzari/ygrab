// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel.DataAnnotations;
using YGrab.Domain;

namespace YGrab.Infrastructure.Configuration;

/// <summary>
/// Where the database and the job files live.
/// </summary>
/// <remarks>
/// A change needs a restart, since the database connections and the job folders use the path
/// from the start.
/// </remarks>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Holds the database and the job folders.
    /// </summary>
    [Required]
    public string DataDirectory { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ygrab"
        );

    /// <summary>
    /// One folder per job.
    /// </summary>
    public string JobsDirectory => Path.Combine(DataDirectory, "jobs");

    internal string JobDirectory(JobId id) =>
        Path.Combine(JobsDirectory, id.Value.ToString("N", CultureInfo.InvariantCulture));

    /// <summary>
    /// The output of the running yt-dlp, emptied before each run.
    /// </summary>
    internal string WorkDirectory(JobId id) => Path.Combine(JobDirectory(id), "work");

    /// <summary>
    /// The audio of the downloaded tracks, named after their video ids.
    /// </summary>
    internal string TracksDirectory(JobId id) => Path.Combine(JobDirectory(id), "tracks");

    /// <summary>
    /// The packaged file.
    /// </summary>
    internal string OutputDirectory(JobId id) => Path.Combine(JobDirectory(id), "out");
}

[OptionsValidator]
internal sealed partial class StorageOptionsValidator : IValidateOptions<StorageOptions>;
