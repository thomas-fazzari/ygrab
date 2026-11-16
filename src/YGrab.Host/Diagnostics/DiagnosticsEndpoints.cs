// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using YGrab.Contracts;
using YGrab.Contracts.Diagnostics;
using YGrab.Engine;
using YGrab.Engine.Diagnostics;
using YGrab.Host.Auth;
using YGrab.Host.Events;

namespace YGrab.Host.Diagnostics;

internal static class DiagnosticsEndpoints
{
    // The file of a generated token is the only place its value belongs
    private static readonly string _tokenCategory = typeof(TokenStore).FullName!;

    extension(IServiceCollection services)
    {
        /// <summary>
        /// Keeps the log lines of the server for the log stream (YGrab's debug lines included).
        /// </summary>
        public IServiceCollection AddYGrabDiagnostics()
        {
            services.AddSingleton<LogBuffer>();
            services.AddSingleton<ILoggerProvider>(provider =>
                provider.GetRequiredService<LogBuffer>()
            );
            services.AddLogging(logging =>
                logging
                    .AddFilter<LogBuffer>(nameof(YGrab), LogLevel.Debug)
                    .AddFilter<LogBuffer>(_tokenCategory, LogLevel.None)
            );
            return services;
        }
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapDiagnostics()
        {
            endpoints.MapGet("/status", GetStatusAsync);
            endpoints.MapGet("/logs", Stream);
            return endpoints;
        }
    }

    private static async Task<Ok<StatusDto>> GetStatusAsync(
        IQueryHandler<GetStatus.Query, GetStatus.Status> getStatus,
        CancellationToken cancellationToken
    )
    {
        var status = await getStatus.HandleAsync(new GetStatus.Query(), cancellationToken);
        return TypedResults.Ok(
            new StatusDto(
                status.Usage.StartedAt,
                ImmutableArray.CreateRange(status.Components, component => new ComponentDto(
                    component.Name,
                    component.Version
                )),
                status.Usage.DataBytes,
                status.Usage.FreeBytes
            )
        );
    }

    private static ServerSentEventsResult<string> Stream(
        LogBuffer buffer,
        IHostApplicationLifetime lifetime,
        TimeProvider timeProvider,
        HttpResponse response,
        CancellationToken cancellationToken
    ) =>
        TypedResults.ServerSentEvents(
            RelayAsync(buffer, lifetime, timeProvider, response, cancellationToken)
        );

    private static async IAsyncEnumerable<SseItem<string>> RelayAsync(
        LogBuffer buffer,
        IHostApplicationLifetime lifetime,
        TimeProvider timeProvider,
        HttpResponse response,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        using var subscription = buffer.Subscribe();
        // Ending the subscription ends the stream, so the host does not wait for the client
        await using var stop = lifetime.ApplicationStopping.Register(subscription.Dispose);
        await EventStream.OpenAsync(response, cancellationToken);

        yield return new SseItem<string>(
            JsonSerializer.Serialize(
                subscription.Backlog,
                ApiJsonContext.Default.ImmutableArrayLogEntryDto
            ),
            LogEventNames.Snapshot
        );

        await foreach (
            var item in EventStream.FollowAsync(
                subscription.Reader,
                entry => new SseItem<string>(
                    JsonSerializer.Serialize(entry, ApiJsonContext.Default.LogEntryDto),
                    LogEventNames.Entry
                ),
                response,
                timeProvider,
                cancellationToken
            )
        )
        {
            yield return item;
        }
    }
}
