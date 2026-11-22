// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Every job in memory, finished ones included.
/// </summary>
/// <remarks>
/// <para>
/// Each change swaps the map and publishes under one lock, so the changes of a job keep their
/// order.
/// </para>
/// <para>
/// The store write of an async change runs after the lock, since no lock covers I/O. The write
/// reads the job when it runs, so the store ends with the newest job whatever order the writes
/// run in.
/// </para>
/// </remarks>
internal sealed class JobRegistry(ChangeFeed feed, IJobStore store)
{
    private readonly Lock _lock = new();
    private ImmutableDictionary<JobId, Job> _jobs = [];
    private long _lastRank;

    public Job? Get(JobId id) => Volatile.Read(ref _jobs).GetValueOrDefault(id);

    /// <returns>The newest other job that covers the request of the job, or null when the job forces its link.</returns>
    public Job? FindCover(Job job) => FindCover(job, other => other.Covers(job.Request));

    /// <returns>The newest other job that covers the job once it resolves to the release, or null when the job forces its link.</returns>
    public Job? FindCover(Job job, Release release)
    {
        var resolved = job.Resolve(release);
        return FindCover(job, other => other.Covers(resolved));
    }

    public IEnumerable<Job> All() => Volatile.Read(ref _jobs).Values;

    public Job? Active() => Volatile.Read(ref _jobs).Values.FirstOrDefault(job => job.IsActive);

    public Job? NextQueued() => Queued().FirstOrDefault();

    public IEnumerable<Job> Queued() =>
        Volatile.Read(ref _jobs).Values.Where(job => job.State is JobState.Queued).InRunOrder();

    /// <returns>A rank above every rank given or loaded so far.</returns>
    public double NextRank() => Interlocked.Increment(ref _lastRank);

    /// <returns>The newest jobs first.</returns>
    public ImmutableArray<Job> List(int limit) =>
        [
            .. Volatile
                .Read(ref _jobs)
                .Values.OrderByDescending(job => job.CreatedAt)
                .ThenByDescending(job => job.Id)
                .Take(limit),
        ];

    public void Load(ImmutableArray<Job> jobs)
    {
        lock (_lock)
        {
            _jobs = jobs.ToImmutableDictionary(job => job.Id);
            var last = jobs.IsEmpty ? 0 : jobs.Max(job => job.QueueRank);
            Interlocked.Exchange(ref _lastRank, (long)Math.Ceiling(last));
        }
    }

    public async ValueTask AddAsync(Job job, CancellationToken cancellationToken)
    {
        await store.InsertAsync(job, cancellationToken);
        Add(job);
    }

    /// <summary>
    /// Applies <paramref name="change"/> to the job and publishes the result when it differs.
    /// </summary>
    /// <remarks>
    /// The change runs under the lock, so a registry read inside it sees no concurrent change.
    /// </remarks>
    /// <returns>The job after the change, or null when the job is gone.</returns>
    public Job? Update(JobId id, Func<Job, Job> change) => Apply(id, change).Job;

    /// <summary>
    /// Like <see cref="Update"/>, then stores the job when it differs.
    /// </summary>
    public async ValueTask<Job?> UpdateAsync(
        JobId id,
        Func<Job, Job> change,
        CancellationToken cancellationToken
    )
    {
        var (updated, changed) = Apply(id, change);
        if (changed)
            await store.SaveAsync(() => Get(id), cancellationToken);

        return updated;
    }

    /// <summary>
    /// Removes the job and publishes <see cref="JobRemoved"/>.
    /// </summary>
    /// <returns>The removed job, or null when the job is gone.</returns>
    public Job? Remove(JobId id)
    {
        lock (_lock)
        {
            if (!_jobs.TryGetValue(id, out var job))
                return null;

            _jobs = _jobs.Remove(id);
            feed.Publish(new JobRemoved(id));
            return job;
        }
    }

    private Job? FindCover(Job job, Func<Job, bool> covers) =>
        job.Force
            ? null
            : Volatile
                .Read(ref _jobs)
                .Values.Where(other => other.Id != job.Id && covers(other))
                .OrderByDescending(other => other.CreatedAt)
                .FirstOrDefault();

    private (Job? Job, bool Changed) Apply(JobId id, Func<Job, Job> change)
    {
        lock (_lock)
        {
            if (!_jobs.TryGetValue(id, out var job))
                return (null, false);

            var updated = change(job);
            if (updated == job)
                return (job, false);

            _jobs = _jobs.SetItem(id, updated);
            feed.Publish(new JobChanged(updated));
            return (updated, true);
        }
    }

    private void Add(Job job)
    {
        lock (_lock)
        {
            _jobs = _jobs.Add(job.Id, job);
            feed.Publish(new JobChanged(job));
        }
    }
}
