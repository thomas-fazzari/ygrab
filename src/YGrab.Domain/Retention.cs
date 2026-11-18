// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// How long a finished job keeps its files, then its place in the history.
/// </summary>
/// <param name="Audio">The retention of the files of an audio job.</param>
/// <param name="Video">The retention of the file of a video job, which is far bigger.</param>
/// <param name="History">How long a finished job stays in the history.</param>
public sealed record Retention(TimeSpan Audio, TimeSpan Video, TimeSpan History)
{
    /// <summary>
    /// Returns when the sweep removes the job from the history, and null for a job that has not
    /// finished.
    /// </summary>
    public DateTimeOffset? RemovedAt(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return job.FinishedAt + History;
    }

    /// <summary>
    /// Returns when the job loses its files under this retention, and null for a job that the
    /// sweep never deletes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For an expired job, the time follows the current retention, so it can differ from the time
    /// that the sweep deleted the files.
    /// </para>
    /// </remarks>
    public DateTimeOffset? ExpiresAt(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return job.State is JobState.Skipped or not JobState.Finished
            ? null
            : job.FinishedAt + (job.Request is JobRequest.Video ? Video : Audio);
    }
}
