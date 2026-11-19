// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// A resolved release and the state of each of its tracks.
/// </summary>
/// <remarks>
/// The tracks always match the tracks of the release, in the same order.
/// </remarks>
public sealed record JobRelease
{
    internal const int FinishedPercent = 100;

    private JobRelease(Release release, ImmutableArray<JobTrack> tracks)
    {
        Release = release;
        Tracks = tracks;
    }

    public Release Release { get; }

    public ImmutableArray<JobTrack> Tracks { get; }

    /// <summary>
    /// The first pending track.
    /// </summary>
    public JobTrack? NextTrack => NextIndex() is var index and >= 0 ? Tracks[index] : null;

    /// <returns>The release with every track pending.</returns>
    public static JobRelease Start(Release release)
    {
        ArgumentNullException.ThrowIfNull(release);

        return new JobRelease(
            release,
            [
                .. release.Tracks.Select(track => new JobTrack(
                    track,
                    TrackState.Pending,
                    Error: null
                )),
            ]
        );
    }

    /// <exception cref="InvalidOperationException">The tracks do not match the release.</exception>
    public static JobRelease Restore(Release release, ImmutableArray<JobTrack> tracks)
    {
        ArgumentNullException.ThrowIfNull(release);

        var matches =
            !tracks.IsDefault
            && tracks.Length == release.Tracks.Length
            && tracks
                .Zip(release.Tracks, (stored, track) =>
                    stored.Track.VideoId == track.VideoId && stored.Track.Number == track.Number
                )
                .All(same => same);
        return matches
            ? new JobRelease(release, tracks)
            : throw new InvalidOperationException("The tracks do not match their release.");
    }

    /// <returns>The index of the next pending track, or -1.</returns>
    internal int NextIndex()
    {
        for (var index = 0; index < Tracks.Length; index++)
        {
            if (Tracks[index].State is TrackState.Pending)
                return index;
        }

        return -1;
    }

    internal JobRelease Mark(int index, TrackState state, string? error) =>
        new(Release, Tracks.SetItem(index, Tracks[index] with { State = state, Error = error }));

    /// <returns>The release with every missing, refused and gone track pending.</returns>
    /// <param name="gone">The video ids whose audio file is gone.</param>
    internal JobRelease Retry(IReadOnlySet<VideoId> gone) =>
        new(
            Release,
            [
                .. Tracks.Select(track =>
                    track.IsMissing
                    || (track.State is TrackState.Done && gone.Contains(track.Track.VideoId))
                        ? track with
                        {
                            State = TrackState.Pending,
                            Error = null,
                        }
                        : track
                ),
            ]
        );

    /// <param name="fraction">The part of the next track that is downloaded, from 0 to 1.</param>
    /// <returns>The progress from 0 to 100.</returns>
    internal int PercentAt(double fraction)
    {
        if (Tracks.IsEmpty)
            return 0;

        var finished = Tracks.Count(track => track.State is not TrackState.Pending);
        return (int)
            Math.Min(FinishedPercent, (finished + fraction) * FinishedPercent / Tracks.Length);
    }
}
