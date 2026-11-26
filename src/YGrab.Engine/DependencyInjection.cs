// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection.Extensions;
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
            AddHandlers(services);
            return services;
        }
    }

    private static void AddHandlers(IServiceCollection services)
    {
        // Skips the open generic decorators, which implement the handler interfaces too
        var handlers = typeof(YGrabEngineServiceCollectionExtensions)
            .Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericType: false });
        foreach (var handler in handlers)
        {
            foreach (var contract in handler.GetInterfaces().Where(IsHandlerInterface))
            {
                var decorator = typeof(TracingDecorator<,>).MakeGenericType(
                    contract.GetGenericArguments()
                );
                services.TryAddSingleton(handler);
                services.AddSingleton(contract, provider =>
                    ActivatorUtilities.CreateInstance(
                        provider,
                        decorator,
                        provider.GetRequiredService(handler)
                    )
                );
            }
        }
    }

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequestHandler<,>);
}
