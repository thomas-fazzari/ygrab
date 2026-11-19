// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;

namespace YGrab.Tests.Fakes;

/// <summary>
/// Keeps the jobs in memory, and a save writes no row for a job that has none, as SQLite does.
/// </summary>
internal sealed class InMemoryJobStore : IJobStore
{
    public Dictionary<Guid, Job> Jobs { get; } = [];

    public Cooldown Cooldown { get; set; } = Cooldown.None;

    public ValueTask<JobSnapshot> LoadAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new JobSnapshot([.. Jobs.Values], Cooldown));

    public ValueTask InsertAsync(Job job, CancellationToken cancellationToken)
    {
        Jobs.Add(job.Id, job);
        return ValueTask.CompletedTask;
    }

    public ValueTask SaveAsync(Func<Job?> current, CancellationToken cancellationToken)
    {
        if (current() is { } job && Jobs.ContainsKey(job.Id))
            Jobs[job.Id] = job;

        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        Jobs.Remove(id);
        return ValueTask.CompletedTask;
    }

    public ValueTask SaveAsync(Cooldown cooldown, CancellationToken cancellationToken)
    {
        Cooldown = cooldown;
        return ValueTask.CompletedTask;
    }
}
