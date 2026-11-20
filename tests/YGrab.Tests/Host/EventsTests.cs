// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using YGrab.Contracts;
using YGrab.Engine;
using YGrab.Engine.Jobs;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Host;

public sealed class EventsTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_event_stream_starts_with_a_snapshot_of_the_jobs()
    {
        await using var factory = new HostFactory();
        var start = factory.Services.GetRequiredService<
            ICommandHandler<StartJob.Command, StartJob.Outcome>
        >();
        var started = await start.HandleAsync(new StartJob.Command(Samples.AlbumUrl), Cancellation);
        var id = started.Value.Should().BeOfType<StartJob.Started>().Subject.Id;
        using var client = factory.CreateBearerClient();
        using var response = await client.GetAsync(
            HostFactory.EventsUrl,
            HttpCompletionOption.ResponseHeadersRead,
            Cancellation
        );
        await using var stream = await response.Content.ReadAsStreamAsync(Cancellation);

        var first = await SseParser
            .Create(stream, (_, data) => Encoding.UTF8.GetString(data))
            .EnumerateAsync(Cancellation)
            .FirstAsync(Cancellation);

        first.EventType.Should().Be(EventNames.Snapshot);
        var snapshot = JsonSerializer.Deserialize(first.Data, ApiJsonContext.Default.SnapshotDto)!;
        snapshot.Jobs.Should().ContainSingle().Which.Id.Should().Be(id.Value);
    }
}
