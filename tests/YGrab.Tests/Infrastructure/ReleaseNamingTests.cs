// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using YGrab.Domain;
using YGrab.Infrastructure.Output;

namespace YGrab.Tests.Infrastructure;

public sealed class ReleaseNamingTests
{
    [Theory]
    [InlineData("AC/DC: Back in Black?", "AC_DC_ Back in Black_")]
    [InlineData("  Title... ", "Title")]
    [InlineData("Title. . ", "Title")]
    [InlineData("...", "_")]
    public void Clean_replaces_invalid_characters_and_trims_the_end(string name, string expected) =>
        ReleaseNaming.Clean(name).Should().Be(expected);

    [Theory]
    [InlineData("Channel", "Channel - Title [vid].mp4")]
    [InlineData(null, "Title [vid].mp4")]
    [InlineData(" ", "Title [vid].mp4")]
    public void A_video_name_has_the_channel_when_there_is_one(string? channel, string expected) =>
        ReleaseNaming
            .VideoName(Samples.Video("vid"), new VideoInfo("Title", channel), ".mp4")
            .Should()
            .Be(expected);

    [Fact]
    public void A_video_name_keeps_its_id_and_extension_after_the_cut()
    {
        var video = new VideoInfo(new string('a', ReleaseNaming.MAX_BYTES * 2), Channel: null);

        var name = ReleaseNaming.VideoName(Samples.Video("vid"), video, ".mkv");

        name.Should().EndWith(" [vid].mkv");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("日本語")]
    [InlineData("a😀")]
    public void Clean_cuts_a_long_name_by_its_bytes_on_a_character_boundary(string unit)
    {
        var name = string.Concat(Enumerable.Repeat(unit, ReleaseNaming.MAX_BYTES));

        var cleaned = ReleaseNaming.Clean(name);

        Encoding.UTF8.GetByteCount(cleaned).Should().BeLessThanOrEqualTo(ReleaseNaming.MAX_BYTES);
        Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(cleaned)).Should().Be(cleaned);
        cleaned.Should().StartWith(unit);
    }
}
