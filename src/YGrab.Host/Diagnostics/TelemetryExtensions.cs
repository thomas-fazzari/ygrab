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
    private const string ENDPOINT_KEY = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string SERVICE_NAME_KEY = "OTEL_SERVICE_NAME";
    private const string DEFAULT_SERVICE_NAME = "ygrab";

    private const string HTTP_CLIENT_SOURCE = "System.Net.Http";

    private static readonly string[] _meters =
    [
        YGrabTelemetry.NAME,
        HTTP_CLIENT_SOURCE,
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
            if (string.IsNullOrWhiteSpace(configuration[ENDPOINT_KEY]))
                return services;

            DistributedContextPropagator.Current =
                DistributedContextPropagator.CreateNoOutputPropagator();
            services
                .AddOpenTelemetry()
                .ConfigureResource(resource =>
                    resource.AddService(
                        configuration[SERVICE_NAME_KEY] ?? DEFAULT_SERVICE_NAME,
                        autoGenerateServiceInstanceId: false
                    )
                )
                .WithTracing(tracing => tracing.AddSource(YGrabTelemetry.NAME, HTTP_CLIENT_SOURCE))
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
