// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// How long a finished job keeps its files.
/// </summary>
/// <param name="Audio">The retention of an audio job.</param>
/// <param name="Video">The retention of a video job, whose file is far bigger.</param>
public sealed record Retention(TimeSpan Audio, TimeSpan Video)
{
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

        return
            job.State
                is not (
                    JobState.Ready
                    or JobState.Incomplete
                    or JobState.Failed
                    or JobState.Expired
                )
            ? null
            : job.FinishedAt + (job.Request is JobRequest.Video ? Video : Audio);
    }
}
