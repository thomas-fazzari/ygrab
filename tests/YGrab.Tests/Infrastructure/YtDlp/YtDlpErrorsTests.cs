// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Infrastructure.YtDlp;

namespace YGrab.Tests.Infrastructure;

public sealed class YtDlpErrorsTests
{
    [Theory]
    [InlineData("HTTP Error 429: Too Many Requests", typeof(RemoteFailure.RateLimited))]
    [InlineData(
        "[youtube] abc: Sign in to confirm you’re not a bot",
        typeof(RemoteFailure.RateLimited)
    )]
    [InlineData(
        "[youtube] abc: Video unavailable. This content isn't available, try again later. The current session has been rate-limited by YouTube for up to an hour.",
        typeof(RemoteFailure.RateLimited)
    )]
    [InlineData(
        "ERROR: [youtube] 98KWcbKbMlk: Video unavailable. YouTube is requiring a captcha challenge before playback",
        typeof(RemoteFailure.RateLimited)
    )]
    [InlineData("[youtube] abc: Video unavailable", typeof(RemoteFailure.Unavailable))]
    [InlineData("[youtube] abc: Private video. Sign in", typeof(RemoteFailure.Unavailable))]
    [InlineData("[youtube] abc: Sign in to confirm your age", typeof(RemoteFailure.Unavailable))]
    [InlineData("Postprocessing: Conversion failed!", typeof(RemoteFailure.Unavailable))]
    [InlineData("unable to download: HTTP Error 403: Forbidden", typeof(RemoteFailure.Transient))]
    [InlineData("The read operation timed out", typeof(RemoteFailure.Transient))]
    [InlineData(null, typeof(RemoteFailure.Transient))]
    public void The_last_error_line_decides_the_kind_of_failure(string? line, Type kind) =>
        YtDlpErrors.Classify(line).Should().BeOfType(kind);
}
