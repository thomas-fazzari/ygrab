// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using Spectre.Console;
using YGrab.Cli.Api;
using YGrab.Contracts.Jobs;

namespace YGrab.Cli.Jobs;

/// <summary>
/// Shows one progress bar per job while the host works on it.
/// </summary>
/// <remarks>
/// With <c>watched</c>, it ends when each of those jobs is finished or gone, and returns the
/// finished ones. Without, it shows every unfinished job until the token is cancelled.
/// </remarks>
internal static class FollowProgress
{
    private const int FullPercent = 100;

    public static async Task<ApiResult<ImmutableDictionary<Guid, JobDto>>> RunAsync(
        IAnsiConsole console,
        ApiClient api,
        IReadOnlySet<Guid>? watched,
        CancellationToken cancellationToken
    )
    {
        var finished = new Dictionary<Guid, JobDto>();
        ApiFailure? lost = null;
        await console
            .Progress()
            .Columns(
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new ProgressBarColumn(),
                new PercentageColumn()
            )
            .StartAsync(async context =>
            {
                var bars = new Dictionary<Guid, ProgressTask>();
                var gone = new HashSet<Guid>();
                var inSnapshot = new HashSet<Guid>();
                await foreach (var update in JobEvents.SubscribeAsync(api, cancellationToken))
                {
                    switch (update)
                    {
                        case JobUpdate.Connected:
                            inSnapshot.Clear();
                            break;
                        case JobUpdate.Changed(var job):
                            inSnapshot.Add(job.Id);
                            Show(context, bars, finished, watched, job);
                            break;
                        case JobUpdate.Removed(var id):
                            gone.Add(id);
                            Stop(bars, id);
                            break;
                        case JobUpdate.Synced:
                            gone.UnionWith(
                                watched?.Where(id =>
                                    !inSnapshot.Contains(id) && !finished.ContainsKey(id)
                                )
                                    ?? []
                            );
                            break;
                        case JobUpdate.Lost(var failure):
                            lost = failure;
                            return;
                    }

                    if (watched?.All(id => finished.ContainsKey(id) || gone.Contains(id)) == true)
                    {
                        return;
                    }
                }
            });

        return lost is null
            ? ApiResult<ImmutableDictionary<Guid, JobDto>>.Ok(finished.ToImmutableDictionary())
            : ApiResult<ImmutableDictionary<Guid, JobDto>>.Fail(lost);
    }

    private static void Show(
        ProgressContext context,
        Dictionary<Guid, ProgressTask> bars,
        Dictionary<Guid, JobDto> finished,
        IReadOnlySet<Guid>? watched,
        JobDto job
    )
    {
        if (watched?.Contains(job.Id) == false)
        {
            return;
        }

        if (!bars.TryGetValue(job.Id, out var bar))
        {
            if (watched is null && job.FinishedAt is not null)
            {
                return;
            }

            bar = context.AddTask(JobView.Progress(job), maxValue: FullPercent);
            bars[job.Id] = bar;
        }

        bar.Description = JobView.Progress(job);
        bar.Value = job.Percent;
        if (job.FinishedAt is not null)
        {
            bar.StopTask();
            finished[job.Id] = job;
        }
    }

    private static void Stop(Dictionary<Guid, ProgressTask> bars, Guid id)
    {
        if (bars.TryGetValue(id, out var bar))
        {
            bar.StopTask();
        }
    }
}
