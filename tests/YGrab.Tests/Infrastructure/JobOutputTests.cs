// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Options;
using YGrab.Engine.Jobs;
using YGrab.Infrastructure.Configuration;
using YGrab.Infrastructure.Output;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Infrastructure;

public sealed class JobOutputTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Packaging_a_video_again_after_the_move_gives_the_same_name_and_file()
    {
        var storage = new StorageOptions { DataDirectory = _temp.FullName };
        var output = new JobOutput(Options.Create(storage));
        var request = Samples.VideoRequest();
        var job = Samples
            .DownloadingVideo(request)
            .CompleteVideo(Samples.VideoDetails)
            .StartPackaging(TestTime.Start);
        var tracks = Directory.CreateDirectory(storage.TracksDirectory(job.Id));
        var source = Path.Combine(tracks.FullName, $"{request.VideoId.Value}.mkv");
        await File.WriteAllTextAsync(source, "video", TestContext.Current.CancellationToken);

        var first = await output.PackageAsync(
            job,
            cover: null,
            details: null,
            TestContext.Current.CancellationToken
        );
        var second = await output.PackageAsync(
            job,
            cover: null,
            details: null,
            TestContext.Current.CancellationToken
        );

        second.Should().Be(first);
        first
            .Should()
            .Be(
                new PackagedFile(
                    ReleaseNaming.VideoName(request.VideoId, Samples.VideoDetails, ".mkv"),
                    "video".Length
                )
            );
        File.Exists(source).Should().BeFalse();
        File.Exists(output.PathOf(job.Id, first.Name)).Should().BeTrue();
    }
}
