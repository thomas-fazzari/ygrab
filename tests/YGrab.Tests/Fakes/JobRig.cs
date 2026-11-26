// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using YGrab.Domain;
using YGrab.Engine;
using YGrab.Engine.Diagnostics;
using YGrab.Engine.Jobs;

namespace YGrab.Tests.Fakes;

/// <summary>
/// The engine use cases wired to the fakes, with a fixed seed for the pauses.
/// </summary>
internal sealed class JobRig
{
    private const int Seed = 1;

    public JobRig(Release release)
    {
        var feed = new ChangeFeed();
        Jobs = new JobRegistry(feed, Store);
        Cooldown = new CooldownHolder(feed, Store);
        var signal = new JobSignal();
        var activeStep = new ActiveStep();
        Catalog = new FakeCatalog(release);
        Start = new StartJob.Handler(Jobs, signal, Time);
        Restore = new RestoreJobs.Handler(Jobs, Cooldown, Store, signal);
        Remove = new RemoveJob.Handler(Jobs, activeStep, Store, Output, signal);
        Expire = new ExpireJobs.Handler(
            Jobs,
            Output,
            Remove,
            Time,
            NullLogger<ExpireJobs.Handler>.Instance
        );
        Retry = new RetryJob.Handler(Jobs, Audio, Videos, signal);
        Move = new MoveJob.Handler(Jobs);
        EndCooldown = new EndCooldown.Handler(Cooldown, signal, Time);
        Advance = new AdvanceJobs.Handler(
            Jobs,
            Cooldown,
            activeStep,
            Catalog,
            Audio,
            Videos,
            new FakeCoverSource(),
            Enricher,
            Output,
            new Random(Seed),
            Time,
            new JobMetrics(FakeMeterFactory.Instance, Jobs, Cooldown, Time),
            NullLogger<AdvanceJobs.Handler>.Instance
        );
    }

    public FakeTimeProvider Time { get; } = new(TestTime.Start);

    public InMemoryJobStore Store { get; } = new();

    public FakeAudioSource Audio { get; } = new();

    public FakeVideoSource Videos { get; } = new();

    public FakeCatalog Catalog { get; }

    public FakeReleaseEnricher Enricher { get; } = new();

    public FakeJobOutput Output { get; } = new();

    public JobRegistry Jobs { get; }

    public CooldownHolder Cooldown { get; }

    public IRequestHandler<StartJob.StartJobRequest, StartJob.Outcome> Start { get; }

    public IRequestHandler<RestoreJobs.RestoreJobsRequest, Done> Restore { get; }

    public IRequestHandler<ExpireJobs.ExpireJobsRequest, Done> Expire { get; }

    public IRequestHandler<RemoveJob.RemoveJobRequest, RemoveJob.Outcome> Remove { get; }

    public IRequestHandler<RetryJob.RetryJobRequest, RetryJob.Outcome> Retry { get; }

    public IRequestHandler<MoveJob.MoveJobRequest, MoveJob.Outcome> Move { get; }

    public IRequestHandler<EndCooldown.EndCooldownRequest, Done> EndCooldown { get; }

    public IRequestHandler<AdvanceJobs.AdvanceJobsRequest, AdvanceJobs.Outcome> Advance { get; }

    public async ValueTask<AdvanceJobs.Outcome> AdvanceAsync(int steps)
    {
        AdvanceJobs.Outcome outcome = default;
        for (var step = 0; step < steps; step++)
        {
            outcome = await Advance.HandleAsync(
                new AdvanceJobs.AdvanceJobsRequest(),
                TestContext.Current.CancellationToken
            );
        }

        return outcome;
    }
}
