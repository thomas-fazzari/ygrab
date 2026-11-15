// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Tests.Domain;

public sealed class VideoLinkTests
{
    public static TheoryData<Uri> Accepted =>
        [
            new Uri("https://www.youtube.com/watch?v=abc123"),
            new Uri("https://youtube.com/watch?v=abc123&list=PL1"),
            new Uri("https://m.youtube.com/watch?v=abc123"),
            new Uri("https://music.youtube.com/watch?v=abc123"),
            new Uri("https://www.youtube.com/shorts/abc123"),
            new Uri("https://www.youtube.com/shorts/abc123/"),
            new Uri("https://www.youtube.com/live/abc123?si=x"),
            new Uri("https://youtu.be/abc123?si=x"),
        ];

    public static TheoryData<Uri> Refused =>
        [
            new Uri("https://example.com/watch?v=abc123"),
            new Uri("ftp://www.youtube.com/watch?v=abc123"),
            new Uri("https://www.youtube.com/watch"),
            new Uri("https://www.youtube.com/@chan/live"),
            new Uri("https://www.youtube.com/browse/MPREb_abc"),
            new Uri("https://music.youtube.com/browse/MPREb_abc"),
            new Uri("https://www.youtube.com/playlist?list=PL123"),
            new Uri("https://www.youtube.com/shorts/"),
            new Uri("https://www.youtube.com/shorts/a/b"),
            new Uri("https://youtu.be/"),
        ];

    [Theory]
    [MemberData(nameof(Accepted))]
    public void A_link_to_one_video_gives_its_id(Uri url) =>
        VideoLink.Parse(url).ShouldSucceed().Value.Should().Be("abc123");

    [Theory]
    [MemberData(nameof(Refused))]
    public void Anything_else_is_refused(Uri url) => VideoLink.Parse(url).ShouldFail();
}
