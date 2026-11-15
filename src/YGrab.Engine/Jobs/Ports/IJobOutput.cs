// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Tags the downloaded tracks of a job, or takes its video, and builds the file that the user
/// downloads.
/// </summary>
public interface IJobOutput
{
    /// <summary>
    /// Packages the tracks that are done: a tagged audio file for a track link, a zip for an album.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A video job has no tags to write, and its kept video file moves to the output without a copy.
    /// </para>
    /// <para>
    /// A run that an earlier crash interrupted can package the same job again, and the result is the
    /// same, including the tags that come from the provider.
    /// </para>
    /// </remarks>
    /// <param name="details">What the provider knows about the release, or null for the tags of YouTube Music only.</param>
    /// <returns>The name that the browser saves, which is not the name on disk.</returns>
    ValueTask<string> PackageAsync(
        Job job,
        Cover? cover,
        ReleaseDetails? details,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// The full path of the packaged file on disk, or null when the file is gone.
    /// </summary>
    /// <param name="fileName">The name that <see cref="PackageAsync"/> returned.</param>
    string? FindFile(Guid jobId, string fileName);

    /// <summary>
    /// Deletes the folder of the job, with every file in it.
    /// </summary>
    void Delete(Guid jobId);
}
