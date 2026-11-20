// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// Where a <see cref="Job"/> is in its life.
/// </summary>
/// <remarks>
/// Each case holds only the data that exists in that state, so a state cannot lack its file or its
/// retry time.
/// </remarks>
public closed record JobState
{
    public sealed record Queued : JobState;

    public sealed record Resolving : JobState;

    public sealed record Downloading : JobState;

    /// <param name="Reason">The failure that the retry follows.</param>
    public sealed record Waiting(DateTimeOffset RetryAt, string Reason) : JobState;

    public sealed record Packaging : JobState;

    /// <summary>
    /// A state that only a retry or the sweep leaves.
    /// </summary>
    public closed record Finished(DateTimeOffset FinishedAt) : JobState;

    /// <summary>
    /// A finished state whose file the client can download.
    /// </summary>
    /// <param name="FileSize">The size in bytes, or null for a job stored without one.</param>
    public closed record WithFile(string FileName, long? FileSize, DateTimeOffset FinishedAt)
        : Finished(FinishedAt);

    public sealed record Ready(string FileName, long? FileSize, DateTimeOffset FinishedAt)
        : WithFile(FileName, FileSize, FinishedAt);

    /// <summary>
    /// The file lacks the tracks that could not be downloaded.
    /// </summary>
    public sealed record Incomplete(string FileName, long? FileSize, DateTimeOffset FinishedAt)
        : WithFile(FileName, FileSize, FinishedAt);

    public sealed record Failed(string Reason, DateTimeOffset FinishedAt) : Finished(FinishedAt);

    /// <param name="DuplicateOf">The job that already covers the link.</param>
    public sealed record Skipped(JobId DuplicateOf, DateTimeOffset FinishedAt)
        : Finished(FinishedAt);

    /// <summary>
    /// The file is deleted after its retention.
    /// </summary>
    public sealed record Expired(DateTimeOffset FinishedAt) : Finished(FinishedAt);
}
