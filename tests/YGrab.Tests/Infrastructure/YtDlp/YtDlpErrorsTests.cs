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

    [Theory]
    [InlineData("[youtube] abc: Private video. Sign in", "Private video. Sign in")]
    [InlineData("HTTP Error 429: Too Many Requests", "HTTP Error 429: Too Many Requests")]
    public void The_reason_drops_the_extractor_and_the_video_id(string line, string reason) =>
        YtDlpErrors.Classify(line).Reason.Should().Be(reason);

    [Theory]
    [InlineData(
        "WARNING: [youtube] abc: Unable to download API page: HTTP Error 429: Too Many Requests (caused by <HTTPError 429: Too Many Requests>)",
        typeof(RemoteFailure.RateLimited)
    )]
    [InlineData(
        "WARNING: [youtube] abc: The read operation timed out. Retrying (2/3)...",
        typeof(RemoteFailure.Transient)
    )]
    [InlineData("WARNING: [youtube] abc: Some formats are missing", null)]
    [InlineData("ERROR: [youtube] abc: HTTP Error 429: Too Many Requests", null)]
    public void Only_a_warning_of_a_retry_or_a_rate_limit_has_a_cause(string line, Type? kind) =>
        (YtDlpErrors.WarningCause(line)?.GetType()).Should().Be(kind);
}
