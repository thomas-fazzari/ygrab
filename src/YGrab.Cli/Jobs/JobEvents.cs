// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using YGrab.Cli.Api;
using YGrab.Contracts;
using YGrab.Contracts.Jobs;

namespace YGrab.Cli.Jobs;

internal closed record JobUpdate
{
    /// <summary>
    /// The jobs that the host lists when the stream connects.
    /// </summary>
    public sealed record Snapshot(ImmutableArray<JobDto> Jobs) : JobUpdate;

    public sealed record Changed(JobDto Job) : JobUpdate;

    public sealed record Removed(Guid Id) : JobUpdate;

    public sealed record Lost(ApiFailure Failure) : JobUpdate;
}

/// <summary>
/// Follows the event stream of the host, and connects again when the stream ends.
/// </summary>
/// <remarks>
/// The host ends the stream of a client that falls behind, and the next snapshot brings it up to
/// date. Only a server that stays unreachable ends the sequence.
/// </remarks>
internal static class JobEvents
{
    internal const int MaxReconnects = 5;

    internal static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(2);

    public static async IAsyncEnumerable<JobUpdate> SubscribeAsync(
        ApiClient api,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var failures = 0;
        while (true)
        {
            var opened = await api.OpenEventsAsync(cancellationToken);
            if (!opened.TryGetValue(out var response, out var failure))
            {
                if (failure is not ApiFailure.Unreachable || ++failures > MaxReconnects)
                {
                    yield return new JobUpdate.Lost(failure);
                    yield break;
                }

                await Task.Delay(ReconnectDelay, TimeProvider.System, cancellationToken);
                continue;
            }

            failures = 0;
            using (response)
            {
                await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var events = SseParser
                    .Create(body, (_, data) => Encoding.UTF8.GetString(data))
                    .EnumerateAsync(cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);
                while (await TryMoveNextAsync(events))
                {
                    if (ToUpdate(events.Current) is { } update)
                        yield return update;
                }
            }

            await Task.Delay(ReconnectDelay, TimeProvider.System, cancellationToken);
        }
    }

    private static async ValueTask<bool> TryMoveNextAsync(IAsyncEnumerator<SseItem<string>> events)
    {
        try
        {
            return await events.MoveNextAsync();
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException)
        {
            return false;
        }
    }

    private static JobUpdate? ToUpdate(SseItem<string> item) =>
        item.EventType switch
        {
            EventNames.Snapshot => new JobUpdate.Snapshot(
                JsonSerializer.Deserialize(item.Data, ApiJsonContext.Default.SnapshotDto)!.Jobs
            ),
            EventNames.Job => new JobUpdate.Changed(
                JsonSerializer.Deserialize(item.Data, ApiJsonContext.Default.JobDto)!
            ),
            EventNames.JobRemoved => new JobUpdate.Removed(
                JsonSerializer.Deserialize(item.Data, ApiJsonContext.Default.JobRemovedDto)!.Id
            ),
            _ => null,
        };
}
