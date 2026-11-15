// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Text;
using ATL;
using YGrab.Domain;
using YGrab.Infrastructure.Output;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Infrastructure;

public sealed class TrackTaggerTests : IDisposable
{
    private const string Isrc = "USABC1234567";
    private const int ReleaseYear = 2020;
    private const int ExplicitValue = 1;

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void An_opus_file_gets_the_provider_tags_loses_them_and_gets_them_again_without_duplicates()
    {
        var path = Copy("silence.opus");
        var (release, source, details) = Sample();

        TrackTagger.Write(path, release, source, cover: null, details);
        var enriched = new Track(path);
        var raw = Raw(path);

        enriched.DiscNumber.Should().Be(2);
        enriched.DiscTotal.Should().Be(2);
        enriched.TrackNumber.Should().Be(1);
        enriched.TrackTotal.Should().Be(4);
        enriched.ISRC.Should().Be(Isrc);
        Day(enriched).Should().Be("2019-03-04");
        enriched.AdditionalFields["LABEL"].Should().Be("Lab");
        enriched.AdditionalFields["BARCODE"].Should().Be("0123456789012");
        enriched.AdditionalFields["RELEASETYPE"].Should().Be("album;compilation");
        enriched.AdditionalFields["ORIGINALYEAR"].Should().Be("2020");
        enriched.AdditionalFields["ITUNESADVISORY"].Should().Be("1");
        enriched.AlbumArtist.Should().Be("Artist, Guest");
        raw.Should().Contain("ORIGINALDATE=2020").And.NotContain("ORIGINALDATE =");
        raw.Should().Contain("ALBUMARTISTS=Artist").And.Contain("ALBUMARTISTS=Guest");

        TrackTagger.Write(path, release, source, cover: null, details: null);
        var plain = new Track(path);
        raw = Raw(path);

        plain.DiscNumber.Should().Be(0);
        plain.TrackNumber.Should().Be(source.Number);
        plain.TrackTotal.Should().Be(release.TrackCount);
        plain.ISRC.Should().BeEmpty();
        plain.Year.Should().Be(ReleaseYear);
        plain
            .AdditionalFields.Keys.Should()
            .BeEquivalentTo(["VORBIS-VENDOR", "ITUNESADVISORY", "ARTISTS", "ALBUMARTISTS"]);
        raw.Should().NotContain("ORIGINALDATE").And.NotContain("LABEL");

        TrackTagger.Write(path, release, source, cover: null, details);
        raw = Raw(path);

        raw.AsSpan().Count("LABEL=").Should().Be(1);
        raw.AsSpan().Count("ORIGINALDATE=").Should().Be(1);
        raw.AsSpan().Count("ISRC=").Should().Be(1);
        raw.AsSpan().Count("ALBUMARTISTS=").Should().Be(2);
    }

    [Fact]
    public void An_m4a_file_gets_the_isrc_in_a_freeform_atom_and_the_rating_as_one()
    {
        var path = Copy("silence.m4a");
        var (release, source, details) = Sample();

        TrackTagger.Write(path, release, source, cover: null, details);
        var enriched = new Track(path);
        var raw = Raw(path);

        enriched.ISRC.Should().BeEmpty();
        enriched.AdditionalFields["ISRC"].Should().Be(Isrc);
        enriched.AdditionalFields["ORIGINALDATE"].Should().Be("2020");
        enriched.AdditionalFields["MusicBrainz Album Type"].Should().Be("compilation");
        enriched.AlbumArtist.Should().Be("Artist, Guest");
        enriched.AdditionalFields["LABEL"].Should().Be("Lab");
        enriched.DiscNumber.Should().Be(2);
        Day(enriched).Should().Be("2019-03-04");
        raw.Should().NotContain("\u00a9isr");
        RatingOf(path).Should().Be(ExplicitValue);

        TrackTagger.Write(path, release, source, cover: null, details: null);
        var plain = new Track(path);
        raw = Raw(path);

        plain.DiscNumber.Should().Be(0);
        plain.Year.Should().Be(ReleaseYear);
        plain.AdditionalFields.Keys.Should().BeEmpty();
        raw.Should().NotContain("LABEL").And.NotContain("ISRC").And.NotContain("ORIGINALDATE");
        RatingOf(path).Should().Be(ExplicitValue);

        TrackTagger.Write(path, release, source, cover: null, details);
        raw = Raw(path);

        raw.AsSpan().Count("LABEL").Should().Be(1);
        raw.AsSpan().Count("ISRC").Should().Be(1);
        RatingOf(path).Should().Be(ExplicitValue);
    }

    private static (Release Release, ReleaseTrack Source, ReleaseDetails Details) Sample()
    {
        var release = Samples.Release(2);
        var source = release.Tracks[1];
        var details = new ReleaseDetails(
            "Lab",
            "0123456789012",
            new DateOnly(2019, 3, 4),
            ReleaseType.Compilation,
            2,
            ImmutableDictionary<int, TrackDetails>.Empty.Add(
                source.Number,
                new TrackDetails(Isrc, 2, 1, 4)
            )
        );
        return (release, source, details);
    }

    private string Copy(string fixture)
    {
        var path = Path.Combine(_temp.FullName, fixture);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture), path);
        return path;
    }

    private static string Raw(string path) => Encoding.Latin1.GetString(File.ReadAllBytes(path));

    private static string? Day(Track track) =>
        track.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // The value byte of the rtng atom follows its size, name, type and locale
    private static int RatingOf(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var atom = Encoding.Latin1.GetString(bytes).IndexOf("rtng", StringComparison.Ordinal);
        return bytes[atom + "rtng".Length + 4 + 4 + 4 + 4];
    }
}
