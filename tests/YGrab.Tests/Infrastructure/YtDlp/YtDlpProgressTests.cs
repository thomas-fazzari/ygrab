// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Infrastructure.YtDlp;

namespace YGrab.Tests.Infrastructure;

public sealed class YtDlpProgressTests
{
    [Theory]
    [InlineData("progress 50 100", 0.5)]
    [InlineData("progress 100 1234.5", 100 / 1234.5)]
    [InlineData("progress 500 100", 1)]
    public void A_progress_line_gives_the_downloaded_fraction(string line, double expected)
    {
        YtDlpProgress.TryParseAudio(line, out var fraction).Should().BeTrue();

        fraction.Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData("progress 100 NA")]
    [InlineData("progress NA NA")]
    [InlineData("progress 100 0")]
    [InlineData("file /tmp/a.opus")]
    public void Any_other_line_gives_no_fraction(string line) =>
        YtDlpProgress.TryParseAudio(line, out _).Should().BeFalse();

    [Theory]
    [InlineData("progress avc1.640028 50 100", YtDlpProgress.VideoStreamEnd * 0.5)]
    [InlineData("progress av01.0.08M.08 100 100", YtDlpProgress.VideoStreamEnd)]
    [InlineData(
        "progress none 50 100",
        YtDlpProgress.VideoStreamEnd
            + ((YtDlpProgress.AudioStreamEnd - YtDlpProgress.VideoStreamEnd) * 0.5)
    )]
    [InlineData("progress none 100 100", YtDlpProgress.AudioStreamEnd)]
    public void A_video_line_places_its_stream_in_its_part_of_the_whole(
        string line,
        double expected
    )
    {
        YtDlpProgress.TryParseVideo(line, out var fraction).Should().BeTrue();

        fraction.Should().BeApproximately(expected, 1e-9);
    }

    [Theory]
    [InlineData("progress avc1.640028 100 NA")]
    [InlineData("progress 50 100")]
    [InlineData("progress none")]
    [InlineData("file /tmp/a.mp4")]
    public void Any_other_video_line_gives_no_fraction(string line) =>
        YtDlpProgress.TryParseVideo(line, out _).Should().BeFalse();
}
