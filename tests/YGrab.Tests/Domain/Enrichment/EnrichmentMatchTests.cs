// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using YGrab.Domain;

namespace YGrab.Tests.Domain;

public sealed class EnrichmentMatchTests
{
    [Fact]
    public void A_two_disc_album_with_flat_numbers_gets_the_disc_and_the_isrc_of_each_track()
    {
        var release = ReleaseOf(
            "The Wall",
            "Pink Floyd",
            5,
            YtmTrack(1, "In the Flesh?"),
            YtmTrack(2, "The Thin Ice"),
            YtmTrack(3, "Another Brick in the Wall, Pt. 1"),
            YtmTrack(4, "Hey You"),
            YtmTrack(5, "Comfortably Numb")
        );
        var candidate = CandidateOf(
            "The Wall",
            "Pink Floyd",
            Offered("In the Flesh?", disc: 1, numberInDisc: 1, isrc: "GBN9Y1100079"),
            Offered("The Thin Ice", disc: 1, numberInDisc: 2),
            Offered("Another Brick in the Wall, Pt. 1", disc: 1, numberInDisc: 3),
            Offered("Hey You", disc: 2, numberInDisc: 1, isrc: "GBN9Y1100095"),
            Offered("Comfortably Numb", disc: 2, numberInDisc: 2)
        );

        var result = EnrichmentMatch.Match(release, candidate);

        var details = result.Value.Should().BeOfType<ReleaseDetails>().Subject;
        details.DiscTotal.Should().Be(2);
        details.Tracks[1].Should().Be(new TrackDetails(IsrcOf("GBN9Y1100079"), 1, 1, 3));
        details.Tracks[4].Should().Be(new TrackDetails(IsrcOf("GBN9Y1100095"), 2, 1, 2));
        details.Tracks[5].Should().Be(new TrackDetails(Isrc: null, 2, 2, 2));
    }

    [Fact]
    public void An_incomplete_album_checks_only_its_available_tracks()
    {
        var release = ReleaseOf("Album", "Artist", 3, YtmTrack(1, "One"), YtmTrack(3, "Three"));
        var candidate = CandidateOf(
            "Album",
            "Artist",
            Offered("One"),
            Offered("A title that YouTube Music does not list"),
            Offered("Three")
        );

        var result = EnrichmentMatch.Match(release, candidate);

        result
            .Value.Should()
            .BeOfType<ReleaseDetails>()
            .Which.Tracks.Keys.Should()
            .BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void A_single_track_job_keeps_the_disc_total_of_the_whole_album()
    {
        var release = ReleaseOf("Album", "Artist", 3, YtmTrack(3, "Three"));
        var candidate = CandidateOf(
            "Album",
            "Artist",
            Offered("One", disc: 1, numberInDisc: 1),
            Offered("Two", disc: 1, numberInDisc: 2),
            Offered("Three", disc: 2, numberInDisc: 1)
        );

        var result = EnrichmentMatch.Match(release, candidate);

        var details = result.Value.Should().BeOfType<ReleaseDetails>().Subject;
        details.DiscTotal.Should().Be(2);
        details
            .Tracks.Should()
            .ContainSingle()
            .Which.Value.Should()
            .Be(new TrackDetails(Isrc: null, 2, 1, 1));
    }

    [Theory]
    [InlineData("Song - Remastered 2011", false, 200)]
    [InlineData("Song (Album Version (Explicit))", true, 200)]
    [InlineData("Song (feat. A Guest)", false, 200)]
    [InlineData("The Song", false, 200)]
    [InlineData("Song", false, 205)]
    public void A_track_with_the_same_title_flag_and_duration_is_accepted(
        string title,
        bool isExplicit,
        int seconds
    )
    {
        var release = ReleaseOf(
            "Album",
            "Artist",
            1,
            YtmTrack(1, "Song", explicitFlag: isExplicit)
        );
        var candidate = CandidateOf(
            "Album",
            "Artist",
            Offered(title, seconds: seconds, explicitFlag: isExplicit)
        );

        EnrichmentMatch.Match(release, candidate).Value.Should().BeOfType<ReleaseDetails>();
    }

    [Theory]
    [InlineData("Song (5.1 Mix)", false, 200)]
    [InlineData("Song", true, 200)]
    [InlineData("Song", false, 206)]
    [InlineData("Song", false, 0)]
    public void A_track_with_another_version_flag_or_duration_is_rejected(
        string title,
        bool isExplicit,
        int seconds
    )
    {
        var release = ReleaseOf("Album", "Artist", 1, YtmTrack(1, "Song"));
        var candidate = CandidateOf(
            "Album",
            "Artist",
            Offered(title, seconds: seconds, explicitFlag: isExplicit)
        );

        EnrichmentMatch.Match(release, candidate).Value.Should().BeOfType<Mismatch>();
    }

    [Theory]
    [InlineData("Smashing Pumpkins", "The Smashing Pumpkins", true)]
    [InlineData("The Smashing Pumpkins", "Smashing Pumpkins", true)]
    [InlineData("Smashing Pumpkins", "Pumpkins", false)]
    public void An_artist_matches_whatever_a_leading_the(string mine, string theirs, bool accepted)
    {
        var release = ReleaseOf("Album", mine, 1, YtmTrack(1, "Song"));
        var candidate = CandidateOf("Album", theirs, Offered("Song"));

        EnrichmentMatch
            .Match(release, candidate)
            .Value.Should()
            .BeAssignableTo(accepted ? typeof(ReleaseDetails) : typeof(Mismatch));
    }

    [Theory]
    [InlineData("Help! (Remastered 2009)", "Help!", true)]
    [InlineData("Abbey Road", "Abbey Road (Super Deluxe Edition)", true)]
    [InlineData("Abbey Road (2019 Mix)", "Abbey Road", false)]
    public void An_album_title_matches_once_the_edition_notes_are_stripped(
        string mine,
        string theirs,
        bool accepted
    )
    {
        var release = ReleaseOf(mine, "Artist", 1, YtmTrack(1, "Song"));
        var candidate = CandidateOf(theirs, "Artist", Offered("Song"));

        EnrichmentMatch
            .Match(release, candidate)
            .Value.Should()
            .BeAssignableTo(accepted ? typeof(ReleaseDetails) : typeof(Mismatch));
    }

    [Fact]
    public void A_candidate_with_another_track_count_is_rejected()
    {
        var release = ReleaseOf("Album", "Artist", 2, YtmTrack(1, "One"));
        var candidate = CandidateOf("Album", "Artist", Offered("One"));

        EnrichmentMatch.Match(release, candidate).Value.Should().BeOfType<Mismatch>();
    }

    private static Release ReleaseOf(
        string title,
        string artist,
        int trackCount,
        params ReleaseTrack[] tracks
    ) =>
        new(
            Samples.Album,
            title,
            [artist],
            1979,
            new Uri("https://example.com/cover"),
            trackCount,
            [.. tracks]
        );

    private static ReleaseTrack YtmTrack(int number, string title, bool explicitFlag = false) =>
        new(
            Samples.Video(string.Create(CultureInfo.InvariantCulture, $"v{number}")),
            title,
            ["Artist"],
            number,
            TimeSpan.FromSeconds(200),
            explicitFlag
        );

    private static CandidateRelease CandidateOf(
        string title,
        string artist,
        params CandidateTrack[] tracks
    ) => new(title, artist, tracks.Length, [.. tracks]);

    private static CandidateTrack Offered(
        string title,
        int seconds = 200,
        bool explicitFlag = false,
        int disc = 1,
        int numberInDisc = 1,
        string? isrc = null
    ) => new(title, Durations.FromSeconds(seconds), explicitFlag, disc, numberInDisc, IsrcOf(isrc));

    private static Isrc? IsrcOf(string? value) =>
        value is null ? null : Isrc.Create(value).ShouldSucceed();
}
