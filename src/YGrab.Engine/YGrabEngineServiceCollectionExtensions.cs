// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection.Extensions;
using YGrab.Engine;
using YGrab.Engine.Diagnostics;
using YGrab.Engine.Jobs;

namespace Microsoft.Extensions.DependencyInjection;

public static class YGrabEngineServiceCollectionExtensions
{
    private static readonly Type[] _handlerInterfaces =
    [
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
    ];

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
            AddHandlers(services);
            return services;
        }
    }

    private static void AddHandlers(IServiceCollection services)
    {
        var handlers = typeof(YGrabEngineServiceCollectionExtensions)
            .Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false });
        foreach (var handler in handlers)
        {
            foreach (var contract in handler.GetInterfaces().Where(IsHandlerInterface))
                services.AddSingleton(contract, handler);
        }
    }

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && _handlerInterfaces.Contains(type.GetGenericTypeDefinition());
}
