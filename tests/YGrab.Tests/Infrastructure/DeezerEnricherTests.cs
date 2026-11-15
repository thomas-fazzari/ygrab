// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using YGrab.Domain;
using YGrab.Engine.Jobs;
using YGrab.Infrastructure.Deezer;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Infrastructure;

public sealed class DeezerEnricherTests : IDisposable
{
    private const string SearchBody = """
        {"data":[
          {"id":1,"title":"Album","artist":{"name":"Artist"},"nb_tracks":3},
          {"id":2,"title":"Album (Deluxe Edition)","artist":{"name":"The Artist"},"nb_tracks":2}
        ]}
        """;

    private const string TracksBody = """
        {"data":[
          {"title":"Title 1","duration":203,"explicit_lyrics":false,"disk_number":1,"track_position":1,"isrc":"USAAA1111111"},
          {"title":"Title 2","duration":202,"explicit_lyrics":true,"disk_number":1,"track_position":2,"isrc":""}
        ]}
        """;

    private const string AlbumBody = """
        {"label":"Lab","upc":"0123","release_date":"2019-03-04","record_type":"compile"}
        """;

    private const string QuotaBody = """
        {"error":{"type":"Exception","message":"Quota limit exceeded","code":4}}
        """;

    private readonly FakeHttpClientFactory _http = new(new Uri(DeezerEnricher.BaseAddress));
    private readonly FakeTimeProvider _time = new(TestTime.Start);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public void Dispose() => _http.Dispose();

    [Fact]
    public async Task The_first_album_that_is_the_same_product_gives_its_details()
    {
        _http.Bodies["/search/album"] = SearchBody;
        _http.Bodies["/album/2/tracks"] = TracksBody;
        _http.Bodies["/album/2"] = AlbumBody;

        var result = await Enricher().EnrichAsync(Samples.Release(2), Cancellation);

        var details = result.Value.Should().BeOfType<ReleaseDetails>().Subject;
        details
            .Should()
            .BeEquivalentTo(
                new
                {
                    Label = "Lab",
                    Barcode = "0123",
                    ReleaseDate = new DateOnly(2019, 3, 4),
                    ReleaseType = ReleaseType.Compilation,
                    DiscTotal = 1,
                }
            );
        details.Tracks[1].Should().Be(new TrackDetails("USAAA1111111", 1, 1, 2));
        details.Tracks[2].Should().Be(new TrackDetails(Isrc: null, 1, 2, 2));
        _http.Paths.Should().Equal("/search/album", "/search/album", "/album/2/tracks", "/album/2");
    }

    [Fact]
    public async Task A_provider_without_the_album_gives_no_match()
    {
        _http.Bodies["/search/album"] = """{"data":[]}""";

        var result = await Enricher().EnrichAsync(Samples.Release(2), Cancellation);

        result.Value.Should().Be(new NotEnriched(EnrichmentFailure.NoMatch));
    }

    [Fact]
    public async Task An_error_body_ends_the_lookup_with_a_provider_error()
    {
        _http.Bodies["/search/album"] = QuotaBody;

        var result = await Enricher().EnrichAsync(Samples.Release(2), Cancellation);

        result.Value.Should().Be(new NotEnriched(EnrichmentFailure.ProviderError));
        _http.Paths.Should().ContainSingle();
    }

    [Fact]
    public async Task A_transport_failure_ends_the_lookup_as_unreachable()
    {
        _http.Failure = new HttpRequestException("connection refused");

        var result = await Enricher().EnrichAsync(Samples.Release(2), Cancellation);

        result.Value.Should().Be(new NotEnriched(EnrichmentFailure.Unreachable));
        _http.Paths.Should().ContainSingle();
    }

    [Fact]
    public async Task A_lookup_that_outlasts_the_time_limit_is_unreachable()
    {
        _http.Hangs = true;
        var lookup = Enricher().EnrichAsync(Samples.Release(2), Cancellation).AsTask();
        await _http.Hanging.Task;

        _time.Advance(DeezerEnricher.EnrichTimeout);

        (await lookup).Value.Should().Be(new NotEnriched(EnrichmentFailure.Unreachable));
    }

    private DeezerEnricher Enricher() => new(_http, _time, NullLogger<DeezerEnricher>.Instance);
}
