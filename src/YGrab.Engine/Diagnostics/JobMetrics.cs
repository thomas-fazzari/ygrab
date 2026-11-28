// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.Metrics;
using YGrab.Domain;
using YGrab.Engine.Jobs;

namespace YGrab.Engine.Diagnostics;

internal sealed class JobMetrics
{
    private const string STEP_TAG = "ygrab.step";
    private const string JOB_ID_TAG = "ygrab.job.id";
    private const string JOB_STATE_TAG = "ygrab.job.state";
    private const string ENRICHMENT_OUTCOME_TAG = "ygrab.enrichment.outcome";

    private const string QUEUED_STATE = "queued";
    private const string RESOLVING_STATE = "resolving";
    private const string DOWNLOADING_STATE = "downloading";
    private const string WAITING_STATE = "waiting";
    private const string PACKAGING_STATE = "packaging";
    private const string READY_STATE = "ready";
    private const string INCOMPLETE_STATE = "incomplete";
    private const string FAILED_STATE = "failed";
    private const string SKIPPED_STATE = "skipped";
    private const string EXPIRED_STATE = "expired";

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
        QUEUED_STATE,
        RESOLVING_STATE,
        DOWNLOADING_STATE,
        WAITING_STATE,
        PACKAGING_STATE,
        READY_STATE,
        INCOMPLETE_STATE,
        FAILED_STATE,
        SKIPPED_STATE,
        EXPIRED_STATE,
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
        _meter = meterFactory.Create(YGrabTelemetry.NAME);
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

    public StepRun StartStep(JobId jobId, JobStep step)
    {
        var kind = StepKind(step);
        var activity = YGrabTelemetry.Source.StartActivity(kind);
        activity?.SetTag(STEP_TAG, kind);
        activity?.SetTag(JOB_ID_TAG, jobId.Value);
        return new StepRun(this, kind, activity, _timeProvider.GetTimestamp());
    }

    /// <param name="failure">The failure of the lookup, or null for a match.</param>
    public void RecordEnrichment(EnrichmentFailure? failure) =>
        _enrichmentLookups.Add(
            1,
            new KeyValuePair<string, object?>(ENRICHMENT_OUTCOME_TAG, EnrichmentOutcome(failure))
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
            new KeyValuePair<string, object?>(JOB_STATE_TAG, state)
        ));
    }

    private static string StateCode(JobState state) =>
        state switch
        {
            JobState.Queued => QUEUED_STATE,
            JobState.Resolving => RESOLVING_STATE,
            JobState.Downloading => DOWNLOADING_STATE,
            JobState.Waiting => WAITING_STATE,
            JobState.Packaging => PACKAGING_STATE,
            JobState.Ready => READY_STATE,
            JobState.Incomplete => INCOMPLETE_STATE,
            JobState.Failed => FAILED_STATE,
            JobState.Skipped => SKIPPED_STATE,
            JobState.Expired => EXPIRED_STATE,
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
            var tags = new TagList { { STEP_TAG, kind } };
            if (_errorType is not null)
                tags.Add(YGrabTelemetry.ERROR_TYPE_TAG, _errorType);

            metrics._stepDuration.Record(
                metrics._timeProvider.GetElapsedTime(started).TotalSeconds,
                tags
            );
            activity?.Dispose();
        }
    }
}
