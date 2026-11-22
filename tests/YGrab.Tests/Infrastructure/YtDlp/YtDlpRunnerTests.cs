// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YGrab.Domain;
using YGrab.Infrastructure.Configuration;
using YGrab.Infrastructure.YtDlp;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Infrastructure.YtDlp;

public sealed class YtDlpRunnerTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact(Timeout = 10_000)]
    public async Task A_run_leaves_no_copy_of_the_cookies_in_the_work_folder()
    {
        var cookies = await WriteCookiesAsync();
        var work = Path.Combine(_temp.FullName, "work");
        // true exits with 0 whatever the arguments, so it stands in for yt-dlp
        var runner = Runner(new YtDlpOptions { Executable = "true", CookiesFile = cookies });

        var failure = await runner.RunAsync(
            work,
            Samples.Video("v1"),
            TimeSpan.FromMinutes(1),
            [],
            _ => { },
            TestContext.Current.CancellationToken
        );

        failure.Should().BeNull();
        Directory.EnumerateFiles(work).Should().BeEmpty();
    }

    [Fact(Timeout = 10_000)]
    public async Task A_process_that_does_not_start_leaves_no_copy_of_the_cookies()
    {
        var cookies = await WriteCookiesAsync();
        var work = Path.Combine(_temp.FullName, "work");
        var runner = Runner(
            new YtDlpOptions { Executable = "ygrab-missing-executable", CookiesFile = cookies }
        );

        var run = async () =>
            await runner.RunAsync(
                work,
                Samples.Video("v1"),
                TimeSpan.FromMinutes(1),
                [],
                _ => { },
                TestContext.Current.CancellationToken
            );

        await run.Should().ThrowAsync<Win32Exception>();
        Directory.EnumerateFiles(work).Should().BeEmpty();
    }

    [Fact(Timeout = 10_000)]
    [UnsupportedOSPlatform("windows")]
    public async Task An_error_after_a_carriage_return_keeps_its_reason()
    {
        var script = Path.Combine(_temp.FullName, "yt-dlp");
        await File.WriteAllTextAsync(
            script,
            """
            #!/bin/sh
            printf 'ERROR: \r[download] Got error: HTTP Error 503: Service Unavailable. Giving up after 10 retries\n' >&2
            exit 1
            """,
            TestContext.Current.CancellationToken
        );
        File.SetUnixFileMode(
            script,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        );
        var runner = Runner(new YtDlpOptions { Executable = script });

        var failure = await runner.RunAsync(
            Path.Combine(_temp.FullName, "work"),
            Samples.Video("v1"),
            TimeSpan.FromMinutes(1),
            [],
            _ => { },
            TestContext.Current.CancellationToken
        );

        failure
            .Should()
            .Be(
                new RemoteFailure.Transient(
                    "HTTP Error 503: Service Unavailable. Giving up after 10 retries"
                )
            );
    }

    private async Task<string> WriteCookiesAsync()
    {
        var cookies = Path.Combine(_temp.FullName, "cookies.txt");
        await File.WriteAllTextAsync(
            cookies,
            "# Netscape HTTP Cookie File",
            TestContext.Current.CancellationToken
        );
        return cookies;
    }

    private static YtDlpRunner Runner(YtDlpOptions settings)
    {
        using var services = new ServiceCollection()
            .Configure<YtDlpOptions>(options =>
            {
                options.Executable = settings.Executable;
                options.CookiesFile = settings.CookiesFile;
            })
            .BuildServiceProvider();
        var options = new LastValidOptions<YtDlpOptions>(
            services.GetRequiredService<IOptionsMonitor<YtDlpOptions>>(),
            NullLogger<LastValidOptions<YtDlpOptions>>.Instance
        );
        return new YtDlpRunner(
            options,
            TimeProvider.System,
            FakeMeterFactory.Instance,
            NullLogger<YtDlpRunner>.Instance
        );
    }
}
