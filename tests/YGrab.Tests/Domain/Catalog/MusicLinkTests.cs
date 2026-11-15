// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Tests.Domain;

public sealed class MusicLinkTests
{
    public static TheoryData<Uri, Type> Accepted =>
        new()
        {
            { new("https://music.youtube.com/browse/MPREb_abc-_1"), typeof(AlbumLink) },
            { new("https://music.youtube.com/browse/MPREb_abc-_1/"), typeof(AlbumLink) },
            {
                new("https://music.youtube.com/playlist?list=OLAK5uy_abc"),
                typeof(AlbumPlaylistLink)
            },
            { new("https://music.youtube.com/watch?v=abc123&list=OLAK5uy_abc"), typeof(TrackLink) },
            { new("https://www.youtube.com/watch?v=abc123"), typeof(TrackLink) },
            { new("https://m.youtube.com/watch?v=abc123"), typeof(TrackLink) },
            { new("https://youtu.be/abc123?si=x"), typeof(TrackLink) },
            { new("https://youtu.be/abc123/"), typeof(TrackLink) },
        };

    public static TheoryData<Uri> Refused =>
        [
            new Uri("https://example.com/watch?v=abc123"),
            new Uri("ftp://music.youtube.com/watch?v=abc123"),
            new Uri("https://music.youtube.com/browse/UC123"),
            new Uri("https://music.youtube.com/playlist?list=PL123"),
            new Uri("https://music.youtube.com/watch?v=a%20b"),
            new Uri("https://music.youtube.com/watch"),
            new Uri("https://youtu.be/"),
        ];

    [Theory]
    [MemberData(nameof(Accepted))]
    public void An_album_or_track_link_parses_to_its_kind(Uri url, Type kind) =>
        MusicLink.Parse(url).ShouldSucceed().Should().BeOfType(kind);

    [Theory]
    [MemberData(nameof(Refused))]
    public void Anything_else_is_refused(Uri url) => MusicLink.Parse(url).ShouldFail();
}
