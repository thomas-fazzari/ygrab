// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.AspNetCore.HttpOverrides;

namespace YGrab.Host.Proxy;

internal static class ProxyExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddTrustedProxies()
        {
            services
                .AddOptions<ProxyOptions>()
                .BindConfiguration(
                    ProxyOptions.SECTION_NAME,
                    HostConfigurationExtensions.FailOnUnknownKeys
                )
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<ProxyOptions>, ProxyOptionsValidator>();
            services
                .AddOptions<ForwardedHeadersOptions>()
                .Configure<IOptions<ProxyOptions>>(
                    (options, proxy) =>
                    {
                        options.ForwardedHeaders =
                            ForwardedHeaders.XForwardedFor
                            | ForwardedHeaders.XForwardedProto
                            | ForwardedHeaders.XForwardedHost;
                        options.KnownIPNetworks.Clear();
                        options.KnownProxies.Clear();
                        foreach (var network in proxy.Value.Networks)
                        {
                            options.KnownIPNetworks.Add(network);
                        }
                    }
                );
            return services;
        }
    }
}
