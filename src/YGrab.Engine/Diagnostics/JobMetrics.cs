// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.Metrics;
using YGrab.Domain;
using YGrab.Engine.Jobs;

namespace YGrab.Engine.Diagnostics;

internal sealed class JobMetrics
{
    private const string StepTag = "ygrab.step";
    private const string JobIdTag = "ygrab.job.id";
    private const string JobStateTag = "ygrab.job.state";
    private const string EnrichmentOutcomeTag = "ygrab.enrichment.outcome";

    private const string QueuedState = "queued";
    private const string ResolvingState = "resolving";
    private const string DownloadingState = "downloading";
    private const string WaitingState = "waiting";
    private const string PackagingState = "packaging";
    private const string ReadyState = "ready";
    private const string IncompleteState = "incomplete";
    private const string FailedState = "failed";
    private const string SkippedState = "skipped";
    private const string ExpiredState = "expired";

    private static readonly double[] _stepBoundaries =
    [
        0.25,
        0.5,
        1,
        2.5,
        5,
        10,
        30,
        60,
        120,
        300,
        600,
        1800,
    ];

    // Each state reports a count, 0 included, so a state that empties drops to 0 in a chart
    private static readonly ImmutableArray<string> _states =
    [
        QueuedState,
        ResolvingState,
        DownloadingState,
        WaitingState,
        PackagingState,
        ReadyState,
        IncompleteState,
        FailedState,
        SkippedState,
        ExpiredState,
    ];

    // The factory owns the meter and disposes it
    private readonly Meter _meter;
    private readonly Histogram<double> _stepDuration;
    private readonly Counter<long> _enrichmentLookups;
    private readonly TimeProvider _timeProvider;

    public JobMetrics(
        IMeterFactory meterFactory,
        JobRegistry jobs,
        CooldownHolder cooldown,
        TimeProvider timeProvider
    )
    {
        _timeProvider = timeProvider;
        _meter = meterFactory.Create(YGrabTelemetry.Name);
        _stepDuration = _meter.CreateHistogram(
            "ygrab.job.step.duration",
            unit: "s",
            description: "The duration of a job step.",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = _stepBoundaries }
        );
        _enrichmentLookups = _meter.CreateCounter<long>(
            "ygrab.enrichment.lookups",
            unit: "{lookup}",
            description: "The lookups of a release at the provider of the extra tags."
        );
        _meter.CreateObservableGauge(
            "ygrab.cooldown.strikes",
            () => cooldown.Current.Strikes,
            unit: "{strike}",
            description: "The rate limits since the last packaged job. 0 means no cooldown."
        );
        _meter.CreateObservableUpDownCounter(
            "ygrab.jobs",
            () => CountJobs(jobs),
            unit: "{job}",
            description: "The jobs that the server holds, by state."
        );
    }

    public StepRun StartStep(Guid jobId, JobStep step)
    {
        var kind = StepKind(step);
        var activity = YGrabTelemetry.Source.StartActivity(kind);
        activity?.SetTag(StepTag, kind);
        activity?.SetTag(JobIdTag, jobId);
        return new StepRun(this, kind, activity, _timeProvider.GetTimestamp());
    }

    /// <param name="failure">The failure of the lookup, or null for a match.</param>
    public void RecordEnrichment(EnrichmentFailure? failure) =>
        _enrichmentLookups.Add(
            1,
            new KeyValuePair<string, object?>(EnrichmentOutcomeTag, EnrichmentOutcome(failure))
        );

    private static string StepKind(JobStep step) =>
        step switch
        {
            JobStep.Resolve => "resolve",
            JobStep.DownloadTrack => "download_track",
            JobStep.DownloadVideo => "download_video",
            JobStep.Package => "package",
        };

    private static string EnrichmentOutcome(EnrichmentFailure? failure) =>
        failure switch
        {
            null => "matched",
            EnrichmentFailure.NoMatch => "no_match",
            EnrichmentFailure.Unreachable => "unreachable",
            EnrichmentFailure.ProviderError => "provider_error",
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, message: null),
        };

    private static IEnumerable<Measurement<int>> CountJobs(JobRegistry jobs)
    {
        var counts = jobs.All()
            .CountBy(job => StateCode(job.State), StringComparer.Ordinal)
            .ToDictionary(StringComparer.Ordinal);
        return _states.Select(state => new Measurement<int>(
            counts.GetValueOrDefault(state),
            new KeyValuePair<string, object?>(JobStateTag, state)
        ));
    }

    private static string StateCode(JobState state) =>
        state switch
        {
            JobState.Queued => QueuedState,
            JobState.Resolving => ResolvingState,
            JobState.Downloading => DownloadingState,
            JobState.Waiting => WaitingState,
            JobState.Packaging => PackagingState,
            JobState.Ready => ReadyState,
            JobState.Incomplete => IncompleteState,
            JobState.Failed => FailedState,
            JobState.Skipped => SkippedState,
            JobState.Expired => ExpiredState,
        };

    /// <summary>
    /// One job step, which records its duration and ends its span when disposed.
    /// </summary>
    public sealed class StepRun(JobMetrics metrics, string kind, Activity? activity, long started)
        : IDisposable
    {
        private string? _errorType;

        public void Fail(string errorType)
        {
            _errorType = errorType;
            YGrabTelemetry.MarkFailed(activity, errorType);
        }

        public void Dispose()
        {
            var tags = new TagList { { StepTag, kind } };
            if (_errorType is not null)
                tags.Add(YGrabTelemetry.ErrorTypeTag, _errorType);

            metrics._stepDuration.Record(
                metrics._timeProvider.GetElapsedTime(started).TotalSeconds,
                tags
            );
            activity?.Dispose();
        }
    }
}
