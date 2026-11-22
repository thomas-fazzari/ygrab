// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// The order in which the runner takes the queued jobs.
/// </summary>
/// <remarks>
/// A job keeps its rank while it runs, so a rate limit or a restart puts it back at its place in
/// the queue.
/// </remarks>
public static class JobQueue
{
    extension(IEnumerable<Job> jobs)
    {
        public IOrderedEnumerable<Job> InRunOrder() =>
            jobs.OrderBy(job => job.QueueRank).ThenBy(job => job.CreatedAt).ThenBy(job => job.Id);
    }

    /// <summary>
    /// A target that runs means the top of the queue.
    /// </summary>
    /// <remarks>
    /// The top is after the active job when that job ranks lower, and below every rank otherwise,
    /// so a retried job never ties with it.
    /// </remarks>
    /// <param name="jobs">Every job, finished ones included.</param>
    /// <returns>Null when the target is not queued or no rank fits between the neighbours.</returns>
    public static double? RankBefore(Job job, JobId target, IReadOnlyCollection<Job> jobs)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(jobs);

        if (target == job.Id)
            return job.QueueRank;

        var active = jobs.FirstOrDefault(other => other.IsActive);
        List<Job> queued =
        [
            .. jobs.Where(other => other.State is JobState.Queued && other.Id != job.Id)
                .InRunOrder(),
        ];
        var index = target == active?.Id ? 0 : queued.FindIndex(other => other.Id == target);
        if (index < 0)
            return null;

        if (index == queued.Count)
            return job.QueueRank;

        var next = queued[index].QueueRank;
        if (index > 0)
            return Between(queued[index - 1].QueueRank, next);

        return active is { QueueRank: var floor } && floor < next
            ? Between(floor, next)
            : jobs.Min(other => other.QueueRank) - 1;
    }

    private static double? Between(double lower, double upper)
    {
        var middle = lower + ((upper - lower) / 2);
        return middle > lower && middle < upper ? middle : null;
    }
}
