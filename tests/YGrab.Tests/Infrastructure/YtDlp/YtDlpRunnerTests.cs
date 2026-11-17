// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        var cookies = Path.Combine(_temp.FullName, "cookies.txt");
        await File.WriteAllTextAsync(
            cookies,
            "# Netscape HTTP Cookie File",
            TestContext.Current.CancellationToken
        );
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
        return new YtDlpRunner(options, TimeProvider.System, NullLogger<YtDlpRunner>.Instance);
    }
}
