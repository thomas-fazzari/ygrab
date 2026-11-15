// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Engine;
using YGrab.Engine.Jobs;

namespace YGrab.Infrastructure.Jobs;

/// <summary>
/// Loads the stored jobs before the sweeper and the runner start.
/// </summary>
internal sealed class JobStartup(ICommandHandler<RestoreJobs.Command, Done> restore)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken) =>
        await restore.HandleAsync(new RestoreJobs.Command(), cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
