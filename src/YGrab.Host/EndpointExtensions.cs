// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using YGrab.Contracts;
using YGrab.Contracts.Jobs;
using YGrab.Host.Auth;
using YGrab.Host.Events;
using YGrab.Host.Health;
using YGrab.Host.Jobs;

namespace YGrab.Host;

internal static class EndpointExtensions
{
    private const string ApiPrefix = "/api";
    private const string IndexFile = "index.html";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddYGrabEndpoints()
        {
            services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
            services.AddProblemDetails();
            services.AddHealthChecks();
            services.AddLoginRateLimit();
            // Host listens behind its proxy only, so it trusts the forwarded headers of any hop
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor
                    | ForwardedHeaders.XForwardedProto
                    | ForwardedHeaders.XForwardedHost;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
            return services;
        }
    }

    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseYGrabHttp()
        {
            app.UseForwardedHeaders();
            app.UseExceptionHandler();
            app.UseStatusCodePages();
            app.UseStaticFiles();
            return app;
        }
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapYGrabEndpoints()
        {
            var api = endpoints.MapGroup(ApiPrefix);
            if (
                !endpoints
                    .ServiceProvider.GetRequiredService<IOptions<AuthOptions>>()
                    .Value.Disabled
            )
            {
                api.RequireAuthorization(AuthExtensions.PolicyName);
            }

            api.AddEndpointFilter<OriginFilter>();
            api.MapJobs();
            api.MapEvents();
            api.MapSession();
            api.MapHealth();
            api.MapFallback("{**rest}", () => TypedResults.NotFound());

            // The fallback of the web client matches after the API fallback, which is more specific
            endpoints.MapFallbackToFile(IndexFile);
            return endpoints;
        }
    }

    // Options of a source-generated context do not apply through the resolver chain
    private static void ConfigureJson(JsonSerializerOptions json)
    {
        json.TypeInfoResolverChain.Insert(0, ApiJsonContext.Default);
        json.TypeInfoResolverChain.Insert(1, HostJsonContext.Default);
        json.RespectNullableAnnotations = true;
        json.RespectRequiredConstructorParameters = true;
        json.Converters.Add(new JsonStringEnumConverter<JobModeCode>(allowIntegerValues: false));
        json.Converters.Add(
            new JsonStringEnumConverter<VideoQualityCode>(allowIntegerValues: false)
        );
    }
}
