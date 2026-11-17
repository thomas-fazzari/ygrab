// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YGrab.Host.Auth;
using YGrab.Host.Diagnostics;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Host;

public sealed class LogBufferTests
{
    [Fact]
    public async Task The_log_buffer_keeps_no_line_of_the_token_store()
    {
        await using var factory = new HostFactory();
        var loggers = factory.Services.GetRequiredService<ILoggerFactory>();
        var buffer = factory.Services.GetRequiredService<LogBuffer>();

        Warn(loggers.CreateLogger<TokenStore>(), "secret-token");
        Warn(loggers.CreateLogger<LogBufferTests>(), "kept");

        using var subscription = buffer.Subscribe();
        subscription.Backlog.Select(entry => entry.Message).Should().Contain("kept");
        subscription.Backlog.Should().NotContain(entry => entry.Message.Contains("secret-token"));
    }

    private static void Warn(ILogger logger, string message) =>
        logger.Log(LogLevel.Warning, default, message, exception: null, (text, _) => text);
}
