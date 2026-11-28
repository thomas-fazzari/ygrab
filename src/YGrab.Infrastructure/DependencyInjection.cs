// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using YGrab.Engine.Diagnostics;
using YGrab.Engine.Jobs;
using YGrab.Infrastructure.Configuration;
using YGrab.Infrastructure.Covers;
using YGrab.Infrastructure.Database;
using YGrab.Infrastructure.Deezer;
using YGrab.Infrastructure.Diagnostics;
using YGrab.Infrastructure.Jobs;
using YGrab.Infrastructure.Output;
using YGrab.Infrastructure.YtDlp;

namespace Microsoft.Extensions.DependencyInjection;

public static class YGrabInfrastructureServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddYGrabInfrastructure()
        {
            services
                .AddOptions<StorageOptions>()
                .BindConfiguration(StorageOptions.SECTION_NAME, FailOnUnknownKeys)
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>();
            services
                .AddOptions<JobsOptions>()
                .BindConfiguration(JobsOptions.SECTION_NAME, FailOnUnknownKeys)
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<JobsOptions>, JobsOptionsValidator>();
            services
                .AddOptions<YtDlpOptions>()
                .BindConfiguration(YtDlpOptions.SECTION_NAME, FailOnUnknownKeys)
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<YtDlpOptions>, YtDlpOptionsValidator>();
            services.TryAddSingleton(TimeProvider.System);

            services.AddSingleton<SqliteDatabase>();
            services.AddSingleton<SchemaMigrator>();
            services.AddSingleton<IJobStore, SqliteJobStore>();
            services.AddSingleton<YtDlpRunner>();
            services.AddSingleton<IAudioSource, YtDlpAudioSource>();
            services.AddSingleton<IVideoSource, YtDlpVideoSource>();

            // ATL settings are process-wide
            ATL.Settings.OutputStacktracesToConsole = false;

            services.AddSingleton<IJobOutput, JobOutput>();
            services.AddSingleton<ICoverSource, HttpCoverSource>();
            services
                .AddHttpClient(HttpCoverSource.CLIENT_NAME, client =>
                    client.MaxResponseContentBufferSize = HttpCoverSource.MAX_RESPONSE_BYTES
                )
                .AddStandardResilienceHandler(RetryExceptTooManyRequests);
            services.AddSingleton<IReleaseEnricher, DeezerEnricher>();
            services
                .AddHttpClient(DeezerEnricher.CLIENT_NAME, client =>
                {
                    client.BaseAddress = new Uri(DeezerEnricher.BASE_ADDRESS);
                    client.DefaultRequestHeaders.UserAgent.Add(
                        new ProductInfoHeaderValue(DeezerEnricher.USER_AGENT, productVersion: null)
                    );
                    client.MaxResponseContentBufferSize = DeezerEnricher.MAX_RESPONSE_BYTES;
                })
                .AddStandardResilienceHandler(RetryExceptTooManyRequests);
            services.AddSingleton(typeof(LastValidOptions<>));
            services.AddSingleton<IRetentionSource, OptionsRetentionSource>();
            services.AddSingleton<IComponentVersions, ToolVersions>();
            services.AddSingleton<IServerUsage, ServerUsageProbe>();

            // Hosts start services in order and stop them in reverse, so the runs stop before the
            // writer that they save through
            AddHostedSingleton<SqliteWriter>(services);
            services.AddHostedService<JobStartup>();
            services.AddHostedService<JobSweeper>();
            services.AddHostedService<JobRuns>();
            return services;
        }
    }

    private static void RetryExceptTooManyRequests(HttpStandardResilienceOptions options) =>
        options.Retry.ShouldHandle = args =>
            ValueTask.FromResult(
                HttpClientResiliencePredicates.IsTransient(args.Outcome)
                    && args.Outcome.Result?.StatusCode is not HttpStatusCode.TooManyRequests
            );

    private static void FailOnUnknownKeys(BinderOptions options) =>
        options.ErrorOnUnknownConfiguration = true;

    private static void AddHostedSingleton<T>(IServiceCollection services)
        where T : class, IHostedService
    {
        services.AddSingleton<T>();
        services.AddHostedService(provider => provider.GetRequiredService<T>());
    }
}
