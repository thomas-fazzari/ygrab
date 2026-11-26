// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using YGrab.Engine.Diagnostics;
using YGrab.Host.Auth;

namespace YGrab.Host.Diagnostics;

internal static class TelemetryExtensions
{
    private const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string ServiceNameKey = "OTEL_SERVICE_NAME";
    private const string DefaultServiceName = "ygrab";

    private const string HttpClientSource = "System.Net.Http";

    private static readonly string[] _meters =
    [
        YGrabTelemetry.Name,
        HttpClientSource,
        "System.Runtime",
        "Microsoft.AspNetCore.Hosting",
        "Polly",
    ];

    extension(IServiceCollection services)
    {
        /// <summary>
        /// Sends the traces, the metrics and the logs over OTLP when an endpoint is set.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The standard <c>OTEL_*</c> variables configure the export, and
        /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> turns it on.
        /// </para>
        /// <para>
        /// The traces hold the job steps and the calls that they make. The HTTP requests that the
        /// server answers have no span, so the event streams and the health checks add none.
        /// </para>
        /// <para>
        /// The calls to Deezer and to the cover hosts carry no trace header.
        /// </para>
        /// </remarks>
        public IServiceCollection AddYGrabTelemetry(IConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration[EndpointKey]))
                return services;

            DistributedContextPropagator.Current =
                DistributedContextPropagator.CreateNoOutputPropagator();
            services
                .AddOpenTelemetry()
                .ConfigureResource(resource =>
                    resource.AddService(
                        configuration[ServiceNameKey] ?? DefaultServiceName,
                        autoGenerateServiceInstanceId: false
                    )
                )
                .WithTracing(tracing => tracing.AddSource(YGrabTelemetry.Name, HttpClientSource))
                .WithMetrics(metrics => metrics.AddMeter(_meters))
                .WithLogging(configureBuilder: null, configureOptions: logging =>
                    logging.IncludeFormattedMessage = true
                )
                .UseOtlpExporter();
            services.AddLogging(logging =>
                logging.AddFilter<OpenTelemetryLoggerProvider>(TokenFile.LogCategory, LogLevel.None)
            );
            return services;
        }
    }
}
