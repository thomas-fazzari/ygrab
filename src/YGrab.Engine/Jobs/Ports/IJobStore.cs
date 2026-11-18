// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Keeps the jobs and the cooldown across restarts.
/// </summary>
public interface IJobStore
{
    ValueTask<JobSnapshot> LoadAsync(CancellationToken cancellationToken);

    ValueTask InsertAsync(Job job, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the whole job that <paramref name="current"/> returns when the write runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The job is read at write time, so two saves that race leave the newest job and a repeated
    /// save is safe.
    /// </para>
    /// <para>
    /// A null job writes nothing. A job that <see cref="DeleteAsync"/> removed stays removed,
    /// since a save writes no row for it.
    /// </para>
    /// </remarks>
    ValueTask SaveAsync(Func<Job?> current, CancellationToken cancellationToken);

    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken);

    ValueTask SaveAsync(Cooldown cooldown, CancellationToken cancellationToken);
}

public sealed record JobSnapshot(ImmutableArray<Job> Jobs, Cooldown Cooldown);
