// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection.Extensions;
using YGrab.Domain;
using YGrab.Engine;
using YGrab.Engine.Diagnostics;
using YGrab.Engine.Jobs;

namespace Microsoft.Extensions.DependencyInjection;

public static class YGrabEngineServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddYGrabEngine()
        {
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(Random.Shared);
            services.AddSingleton<ChangeFeed>();
            services.AddSingleton<JobRegistry>();
            services.AddSingleton<CooldownHolder>();
            services.AddSingleton<JobSignal>();
            services.AddSingleton<ActiveStep>();
            services.AddSingleton<JobMetrics>();
            services.AddSingleton<
                ICommandHandler<StartJob.Command, StartJob.Outcome>,
                StartJob.Handler
            >();
            services.AddSingleton<
                ICommandHandler<RestoreJobs.Command, Done>,
                RestoreJobs.Handler
            >();
            services.AddSingleton<
                ICommandHandler<AdvanceJobs.Command, AdvanceJobs.Outcome>,
                AdvanceJobs.Handler
            >();
            services.AddSingleton<ICommandHandler<ExpireJobs.Command, Done>, ExpireJobs.Handler>();
            services.AddSingleton<
                ICommandHandler<RemoveJob.Command, RemoveJob.Outcome>,
                RemoveJob.Handler
            >();
            services.AddSingleton<
                ICommandHandler<RetryJob.Command, RetryJob.Outcome>,
                RetryJob.Handler
            >();
            services.AddSingleton<
                IQueryHandler<ListJobs.Query, ListJobs.Outcome>,
                ListJobs.Handler
            >();
            services.AddSingleton<IQueryHandler<GetJob.Query, GetJob.Outcome>, GetJob.Handler>();
            services.AddSingleton<
                IQueryHandler<GetJobFile.Query, GetJobFile.Outcome>,
                GetJobFile.Handler
            >();
            services.AddSingleton<
                IQueryHandler<GetCooldown.Query, Cooldown>,
                GetCooldown.Handler
            >();
            services.AddSingleton<
                IQueryHandler<GetStatus.Query, GetStatus.Status>,
                GetStatus.Handler
            >();
            return services;
        }
    }
}
