// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// One request to grab, as an immutable state machine.
/// </summary>
/// <remarks>
/// <para>
/// Each runner step applies one transition, which returns the next state.
/// </para>
/// <para>
/// A step that does not fit the current state is a bug and throws
/// <see cref="InvalidOperationException"/>. <see cref="Progress"/>, <see cref="Requeue"/>,
/// <see cref="Abort"/> and <see cref="Expire"/> return the job unchanged when they do not apply.
/// </para>
/// </remarks>
public sealed record Job
{
    internal const string RefusedMessage = "YouTube refuses the downloads: check yt-dlp";
    internal const string NothingDownloadedMessage = "No track could be downloaded";

    private Job() { }

    public Guid Id { get; private init; }

    public Uri Url { get; private init; } = null!;

    public JobRequest Request { get; private init; } = null!;

    public bool Force { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public JobState State { get; private init; } = null!;

    /// <summary>
    /// The release and its tracks, once the catalog answered.
    /// </summary>
    public JobRelease? Resolved { get; private init; }

    /// <summary>
    /// The video that yt-dlp kept in the files of the job, which a video job holds once it is downloaded.
    /// </summary>
    public VideoInfo? KeptVideo { get; private init; }

    /// <summary>
    /// The failed tries of the current step, which is the resolve or the current track.
    /// </summary>
    public int Attempt { get; private init; }

    /// <summary>
    /// Why the files lack the provider tags, which an audio job holds once it is packaged without them.
    /// </summary>
    public EnrichmentFailure? EnrichmentFailure { get; private init; }

    /// <summary>
    /// The download progress from 0 to 100. It is not stored.
    /// </summary>
    public int Percent { get; private init; }

    public Release? Release => Resolved?.Release;

    public ImmutableArray<JobTrack> Tracks => Resolved?.Tracks ?? [];

    public JobTrack? NextTrack => Resolved?.NextTrack;

    /// <summary>
    /// The request of a video job that is downloading and has no video file yet.
    /// </summary>
    public JobRequest.Video? PendingVideo =>
        State is JobState.Downloading && KeptVideo is null ? Request as JobRequest.Video : null;

    public bool IsActive =>
        State
            is JobState.Resolving
                or JobState.Downloading
                or JobState.Waiting
                or JobState.Packaging;

    public bool CanRetry => State is JobState.Failed or JobState.Incomplete;

    public bool IsFinished =>
        State
            is JobState.Ready
                or JobState.Incomplete
                or JobState.Failed
                or JobState.Skipped
                or JobState.Expired;

    public DateTimeOffset? FinishedAt =>
        State switch
        {
            JobState.Ready ready => ready.FinishedAt,
            JobState.Incomplete incomplete => incomplete.FinishedAt,
            JobState.Failed failed => failed.FinishedAt,
            JobState.Skipped skipped => skipped.FinishedAt,
            JobState.Expired expired => expired.FinishedAt,
            _ => null,
        };

    public static Job Create(Guid id, Uri url, JobRequest request, bool force, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(request);

        return new Job
        {
            Id = id,
            Url = url,
            Request = request,
            Force = force,
            CreatedAt = now,
            State = new JobState.Queued(),
        };
    }

    /// <exception cref="InvalidOperationException">The stored fields break an invariant.</exception>
    public static Job Restore(
        Guid id,
        Uri url,
        JobRequest request,
        bool force,
        DateTimeOffset createdAt,
        JobState state,
        JobRelease? resolved,
        VideoInfo? keptVideo,
        int attempt,
        EnrichmentFailure? enrichmentFailure
    )
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(state);

        var job = new Job
        {
            Id = id,
            Url = url,
            Request = request,
            Force = force,
            CreatedAt = createdAt,
            State = state,
            Resolved = resolved,
            KeptVideo = keptVideo,
            Attempt = attempt,
            EnrichmentFailure = enrichmentFailure,
            Percent = resolved?.PercentAt(0)
                ?? (keptVideo is null ? 0 : JobRelease.FinishedPercent),
        };
        var problem = job switch
        {
            { Attempt: < 0 } => "its attempt is negative",
            { Request: JobRequest.Audio, KeptVideo: not null } => "an audio job has a kept video",
            { Request: JobRequest.Video, EnrichmentFailure: not null } =>
                "a video job has an enrichment failure",
            {
                State: not (JobState.Ready or JobState.Incomplete or JobState.Expired),
                EnrichmentFailure: not null,
            } => $"a {state.GetType().Name} job has an enrichment failure",
            {
                Request: JobRequest.Audio,
                State: JobState.Downloading
                    or JobState.Packaging
                    or JobState.Ready
                    or JobState.Incomplete,
                Resolved: null,
            } => $"a {state.GetType().Name} job has no release",
            { Request: JobRequest.Video, Resolved: not null } => "a video job has a release",
            {
                Request: JobRequest.Video,
                State: JobState.Resolving or JobState.Incomplete or JobState.Skipped,
            } => $"a video job cannot be {state.GetType().Name}",
            {
                Request: JobRequest.Video,
                State: JobState.Packaging or JobState.Ready,
                KeptVideo: null,
            } => $"a {state.GetType().Name} video job has no kept video",
            _ => null,
        };
        return problem is null
            ? job
            : throw new InvalidOperationException($"The stored job {id} is broken: {problem}.");
    }

    /// <summary>
    /// Whether this job already grabs or holds what the request points to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only a queued, active, ready or incomplete job covers a request. An expired, failed or
    /// skipped job covers nothing.
    /// </para>
    /// <para>
    /// A video job covers a video request with the same video id and quality, and an audio job
    /// never covers a video request or the other way round. For two audio jobs:
    /// </para>
    /// <list type="bullet">
    /// <item><description>A track job covers its own link and the link of its resolved track.</description></item>
    /// <item><description>An album or album playlist job covers its own link.</description></item>
    /// <item><description>A resolved album or album playlist job covers the link of its album and the link of a track that it holds.</description></item>
    /// </list>
    /// <para>
    /// A track that is missing is not held. The resolved track of a track job can have another
    /// video id than its link, since a link to a music video resolves to its audio counterpart.
    /// </para>
    /// </remarks>
    public bool Covers(JobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Counts
            && (Request, request) switch
            {
                (JobRequest.Audio, JobRequest.Audio { Link: var link }) => Link is TrackLink
                    ? TrackJobCovers(link)
                    : AlbumJobCovers(link),
                (JobRequest.Video mine, JobRequest.Video other) => mine == other,
                _ => false,
            };
    }

    /// <summary>
    /// Whether this job already grabs or holds everything that a resolved job grabs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only a queued, active, ready or incomplete job covers another job. An expired, failed or
    /// skipped job covers nothing.
    /// </para>
    /// <list type="bullet">
    /// <item><description>A track job covers a track job that holds the same video id.</description></item>
    /// <item><description>A track job never covers an album or album playlist job.</description></item>
    /// <item><description>An album or album playlist job covers an album or album playlist job of the same album.</description></item>
    /// <item><description>An album or album playlist job covers a track job whose track it holds.</description></item>
    /// </list>
    /// <para>
    /// The held video id is the one of the resolved track, which can differ from the id of the link.
    /// </para>
    /// </remarks>
    /// <param name="resolved">A job that has its release.</param>
    public bool Covers(Job resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);

        if (!Counts || Request is not JobRequest.Audio || resolved.Request is not JobRequest.Audio)
        {
            return false;
        }

        return Link is TrackLink
            ? resolved.Link is TrackLink && HeldTrackId is { } held && held == resolved.HeldTrackId
            : AlbumId is { } album
                && album == (resolved.Release?.BrowseId)
                && (
                    resolved.Link is not TrackLink
                    || (resolved.HeldTrackId is { } track && Holds(track))
                );
    }

    public Job Resume(DateTimeOffset now)
    {
        var due = State switch
        {
            JobState.Queued => true,
            JobState.Waiting waiting => waiting.RetryAt <= now,
            _ => false,
        };
        return !due
            ? throw Wrong(nameof(Resume))
            : (
                this with
                {
                    State =
                        Request is JobRequest.Audio && Resolved is null
                            ? new JobState.Resolving()
                            : new JobState.Downloading(),
                }
            );
    }

    public Job Resolve(Release release)
    {
        ArgumentNullException.ThrowIfNull(release);

        return State is not JobState.Resolving
            ? throw Wrong(nameof(Resolve))
            : (
                this with
                {
                    State = new JobState.Downloading(),
                    Resolved = JobRelease.Start(release),
                    Attempt = 0,
                    Percent = 0,
                }
            );
    }

    public Job Skip(Guid duplicateOf, DateTimeOffset now) =>
        State is JobState.Resolving
            ? this with
            {
                State = new JobState.Skipped(duplicateOf, now),
            }
            : throw Wrong(nameof(Skip));

    public Job Progress(double fraction)
    {
        if (
            State is not JobState.Downloading
            || (Resolved is null && PendingVideo is null)
            || !double.IsFinite(fraction)
        )
        {
            return this;
        }

        var at = Math.Clamp(fraction, 0, 1);
        var percent = Math.Max(
            Percent,
            Resolved?.PercentAt(at) ?? (int)(at * JobRelease.FinishedPercent)
        );
        return percent == Percent ? this : this with { Percent = percent };
    }

    /// <param name="info">What yt-dlp reported about the video file that it kept.</param>
    public Job CompleteVideo(VideoInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return PendingVideo is null
            ? throw Wrong(nameof(CompleteVideo))
            : this with
            {
                KeptVideo = info,
                Attempt = 0,
                Percent = JobRelease.FinishedPercent,
            };
    }

    public Job CompleteTrack()
    {
        if (
            State is not JobState.Downloading
            || Resolved is not { } resolved
            || resolved.NextIndex() is not (>= 0 and var index)
        )
        {
            throw Wrong(nameof(CompleteTrack));
        }

        var tracked = resolved.Mark(index, TrackState.Done, error: null);
        return this with
        {
            Resolved = tracked,
            Attempt = 0,
            Percent = Math.Max(Percent, tracked.PercentAt(0)),
        };
    }

    public Job Fail(RemoteFailure failure, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return State is not (JobState.Resolving or JobState.Downloading)
            ? throw Wrong(nameof(Fail))
            : failure switch
            {
                RemoteFailure.RateLimited => this with { State = new JobState.Queued() },
                RemoteFailure.Transient when Attempt + 1 < Backoff.MaxAttempts => this with
                {
                    State = new JobState.Waiting(now + Backoff.Delay(Attempt), failure.Reason),
                    Attempt = Attempt + 1,
                },
                RemoteFailure.Transient => GiveUp(failure.Reason, now, refused: true),
                RemoteFailure.Unavailable => GiveUp(failure.Reason, now, refused: false),
            };
    }

    public Job StartPackaging(DateTimeOffset now)
    {
        if (
            State is not JobState.Downloading
            || PendingVideo is not null
            || Resolved?.NextIndex() >= 0
        )
        {
            throw Wrong(nameof(StartPackaging));
        }

        return Resolved is { } resolved && resolved.Tracks.All(track => track.IsMissing)
            ? Failed(NothingDownloadedMessage, now)
            : this with
            {
                State = new JobState.Packaging(),
            };
    }

    /// <param name="enrichmentFailure">Why the files lack the provider tags, or null when they have them or the job has none to get.</param>
    public Job Finish(string fileName, EnrichmentFailure? enrichmentFailure, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        return
            State is not JobState.Packaging
            || (enrichmentFailure is not null && Request is JobRequest.Video)
            ? throw Wrong(nameof(Finish))
            : (
                this with
                {
                    State = Tracks.Any(track => track.IsMissing)
                        ? new JobState.Incomplete(fileName, now)
                        : new JobState.Ready(fileName, now),
                    EnrichmentFailure = enrichmentFailure,
                    Percent = JobRelease.FinishedPercent,
                }
            );
    }

    public Job Requeue() =>
        State is JobState.Resolving or JobState.Downloading or JobState.Packaging
            ? this with
            {
                State = new JobState.Queued(),
            }
            : this;

    public Job Abort(string reason, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return IsFinished ? this : Failed(reason, now);
    }

    public Job Expire() =>
        State switch
        {
            JobState.Ready ready => this with { State = new JobState.Expired(ready.FinishedAt) },
            JobState.Incomplete incomplete => this with
            {
                State = new JobState.Expired(incomplete.FinishedAt),
            },
            _ => this,
        };

    /// <summary>
    /// Queues the job again with the work that its files do not hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only a failed or incomplete job retries. Any other state throws, so the caller checks
    /// <see cref="CanRetry"/> first.
    /// </para>
    /// <para>
    /// Missing and refused tracks go back to pending, and so do done tracks whose file is gone. A
    /// video job drops its kept video when the file is gone.
    /// </para>
    /// </remarks>
    /// <param name="gone">The video ids whose file is gone, which the caller finds out since the domain does no I/O.</param>
    public Job Retry(IReadOnlySet<VideoId> gone)
    {
        ArgumentNullException.ThrowIfNull(gone);

        if (!CanRetry)
        {
            throw Wrong(nameof(Retry));
        }

        var resolved = Resolved?.Retry(gone);
        var kept =
            Request is JobRequest.Video video && gone.Contains(video.VideoId) ? null : KeptVideo;
        return this with
        {
            State = new JobState.Queued(),
            Resolved = resolved,
            KeptVideo = kept,
            Attempt = 0,
            EnrichmentFailure = null,
            Percent = resolved?.PercentAt(0) ?? (kept is null ? 0 : JobRelease.FinishedPercent),
        };
    }

    private bool Counts => State is not (JobState.Expired or JobState.Failed or JobState.Skipped);

    private MusicLink? Link => (Request as JobRequest.Audio)?.Link;

    private VideoId? HeldTrackId => Tracks.IsEmpty ? null : Tracks[0].Track.VideoId;

    private BrowseId? AlbumId => Release?.BrowseId ?? (Link as AlbumLink)?.BrowseId;

    private bool TrackJobCovers(MusicLink link) =>
        link is TrackLink track && (Link == link || HeldTrackId == track.VideoId);

    private bool AlbumJobCovers(MusicLink link) =>
        link switch
        {
            TrackLink track => Holds(track.VideoId),
            AlbumLink album => Link == link || Release?.BrowseId == album.BrowseId,
            AlbumPlaylistLink => Link == link,
        };

    private bool Holds(VideoId videoId) =>
        Tracks.Any(track => track.Track.VideoId == videoId && !track.IsMissing);

    // Two refused tracks in a row with nothing downloaded yet mean that YouTube refuses the
    // downloads, so the job fails with RefusedMessage
    private Job GiveUp(string reason, DateTimeOffset now, bool refused)
    {
        if (State is JobState.Resolving || Request is JobRequest.Video)
        {
            return Failed(reason, now);
        }

        if (Resolved is not { } resolved || resolved.NextIndex() is not (>= 0 and var index))
        {
            throw Wrong(nameof(Fail));
        }

        var tracked = resolved.Mark(
            index,
            refused ? TrackState.Refused : TrackState.Missing,
            reason
        );
        var missing = this with
        {
            State = new JobState.Downloading(),
            Resolved = tracked,
            Attempt = 0,
            Percent = Math.Max(Percent, tracked.PercentAt(0)),
        };
        return refused && RefusedTwice(tracked, index)
            ? missing.Failed(RefusedMessage, now)
            : missing;
    }

    private static bool RefusedTwice(JobRelease tracked, int index) =>
        index > 0
        && tracked.Tracks[index - 1].State is TrackState.Refused
        && !tracked.Tracks.Any(track => track.State is TrackState.Done);

    private Job Failed(string reason, DateTimeOffset now) =>
        this with
        {
            State = new JobState.Failed(reason, now),
        };

    private InvalidOperationException Wrong(string transition) =>
        new($"{transition} does not apply to job {Id} in state {State.GetType().Name}.");
}
