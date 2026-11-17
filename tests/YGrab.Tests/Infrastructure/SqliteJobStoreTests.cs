// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Time.Testing;
using YGrab.Domain;
using YGrab.Infrastructure.Jobs;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Infrastructure;

public sealed class SqliteJobStoreTests : IAsyncLifetime
{
    private static readonly DateTimeOffset _now = TestTime.Start;

    private readonly FakeTimeProvider _time = new(TestTime.Start);
    private readonly TempDatabase _temp;
    private readonly SqliteJobStore _store;

    public SqliteJobStoreTests()
    {
        _temp = new TempDatabase(_time);
        _store = new SqliteJobStore(_temp.Database, _temp.Writer);
    }

    public async ValueTask InitializeAsync() =>
        await _temp.StartAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => _temp.DisposeAsync();

    [Fact]
    public async Task A_job_of_each_request_and_state_and_the_cooldown_survive_a_round_trip()
    {
        var release = Samples.Release(3);
        Job Mixed() =>
            Samples
                .Downloading(release)
                .CompleteTrack()
                .Fail(new RemoteFailure.Unavailable("private"), _now);
        static Job Kept() =>
            Samples
                .DownloadingVideo(Samples.VideoRequest(quality: VideoQuality.P2160))
                .CompleteVideo(new VideoInfo("Title", Channel: null));
        var jobs = new[]
        {
            Samples.Queued(Samples.AlbumLink()),
            Samples.Queued(Samples.TrackLink("abc")),
            Samples.Queued(new AlbumPlaylistLink(PlaylistId.Create("OLAK5uy_x").ShouldSucceed())),
            Samples.Queued(Samples.AlbumLink()).Resume(_now),
            Samples.Downloading(release).Fail(new RemoteFailure.Transient("timeout"), _now),
            Mixed(),
            Samples.Exhaust(Samples.Downloading(release), _now),
            Mixed().CompleteTrack().StartPackaging(_now),
            Mixed()
                .CompleteTrack()
                .StartPackaging(_now)
                .Finish("a.zip", 2048, enrichmentFailure: null, _now),
            Mixed()
                .CompleteTrack()
                .StartPackaging(_now)
                .Finish("a.zip", 2048, EnrichmentFailure.NoMatch, _now),
            Mixed()
                .CompleteTrack()
                .StartPackaging(_now)
                .Finish("a.zip", 2048, EnrichmentFailure.Unreachable, _now),
            Mixed()
                .CompleteTrack()
                .StartPackaging(_now)
                .Finish("a.zip", 2048, EnrichmentFailure.ProviderError, _now)
                .Expire(),
            Samples.Queued(Samples.AlbumLink()).Resume(_now).Skip(Guid.CreateVersion7(), _now),
            Samples.Queued(Samples.AlbumLink()).Abort("broken", _now),
            Samples.QueuedVideo(Samples.VideoRequest(quality: VideoQuality.Best)),
            Samples.DownloadingVideo(Samples.VideoRequest(quality: VideoQuality.P720)),
            Samples.QueuedVideo(Samples.VideoRequest(quality: VideoQuality.Audio)),
            Kept(),
            Kept().Requeue(),
            Kept().StartPackaging(_now),
            Kept().StartPackaging(_now).Finish("video.mp4", 2048, enrichmentFailure: null, _now),
            Kept()
                .StartPackaging(_now)
                .Finish("video.mp4", 2048, enrichmentFailure: null, _now)
                .Expire(),
            Samples.DownloadingVideo().Fail(new RemoteFailure.Unavailable("private"), _now),
        };
        var cooldown = Cooldown.None.Hit("429", _now);

        foreach (var job in jobs)
        {
            await _store.InsertAsync(job, TestContext.Current.CancellationToken);
        }

        await _store.SaveAsync(cooldown, TestContext.Current.CancellationToken);

        var snapshot = await _store.LoadAsync(TestContext.Current.CancellationToken);

        var loaded = snapshot.Jobs.OrderBy(job => job.Id).ToList();
        var saved = jobs.OrderBy(job => job.Id).ToList();
        loaded.Select(job => job.State).Should().Equal(saved.Select(job => job.State));
        loaded
            .Select(JobRowMapper.ToRow)
            .Should()
            .BeEquivalentTo(saved.Select(JobRowMapper.ToRow), options =>
                options.ComparingRecordsByMembers()
            );
        snapshot.Cooldown.Should().Be(cooldown);
    }

    [Fact]
    public async Task Saving_a_job_again_replaces_its_row()
    {
        var job = Samples.Queued(Samples.AlbumLink());
        await _store.InsertAsync(job, TestContext.Current.CancellationToken);
        await _store.SaveAsync(job.Resume(_now), TestContext.Current.CancellationToken);

        var snapshot = await _store.LoadAsync(TestContext.Current.CancellationToken);

        snapshot.Jobs.Should().ContainSingle().Which.State.Should().BeOfType<JobState.Resolving>();
        snapshot.Cooldown.Should().Be(Cooldown.None);
    }

    [Fact]
    public async Task A_save_after_a_delete_leaves_no_row()
    {
        var job = Samples.Queued(Samples.AlbumLink());
        await _store.InsertAsync(job, TestContext.Current.CancellationToken);
        await _store.DeleteAsync(job.Id, TestContext.Current.CancellationToken);

        await _store.SaveAsync(job.Resume(_now), TestContext.Current.CancellationToken);

        var snapshot = await _store.LoadAsync(TestContext.Current.CancellationToken);
        snapshot.Jobs.Should().BeEmpty();
    }
}
