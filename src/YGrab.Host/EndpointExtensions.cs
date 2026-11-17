// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using YGrab.Contracts;
using YGrab.Contracts.Jobs;
using YGrab.Host.Auth;
using YGrab.Host.Diagnostics;
using YGrab.Host.Events;
using YGrab.Host.Health;
using YGrab.Host.Jobs;
using YGrab.Host.Proxy;

namespace YGrab.Host;

internal static class EndpointExtensions
{
    internal const string ApiPrefix = "/api";
    private const string IndexFile = "index.html";

    // The web build names each file under it after a hash of its content
    private const string HashedAssets = "/assets";
    private const string CacheForever = "public, max-age=31536000, immutable";
    private const string CheckEachTime = "no-cache";

    private static readonly StaticFileOptions _webFiles = new() { OnPrepareResponse = SetCaching };

    extension(IServiceCollection services)
    {
        public IServiceCollection AddYGrabEndpoints()
        {
            services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
            services.AddProblemDetails();
            services.AddHealthChecks();
            services.AddTrustedProxies();
            services.Configure<KestrelServerOptions>(options => options.AddServerHeader = false);
            return services;
        }
    }

    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseYGrabHttp()
        {
            app.UseForwardedHeaders();
            app.UseHsts();
            app.UseSecurityHeaders();
            app.UseExceptionHandler();
            app.UseStatusCodePages();
            app.UseStaticFiles(_webFiles);
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
            api.MapDiagnostics();
            api.MapSession();
            api.MapHealth();
            api.MapFallback("{**rest}", () => TypedResults.NotFound());

            // The fallback of the web client matches after the API fallback, which is more specific
            endpoints.MapFallbackToFile(IndexFile, _webFiles);
            return endpoints;
        }
    }

    // A hashed file never changes
    // The browser revalidates any other file, so a new build applies at once
    private static void SetCaching(StaticFileResponseContext context) =>
        context.Context.Response.Headers.CacheControl =
            context.Context.Request.Path.StartsWithSegments(HashedAssets, StringComparison.Ordinal)
                ? CacheForever
                : CheckEachTime;

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
