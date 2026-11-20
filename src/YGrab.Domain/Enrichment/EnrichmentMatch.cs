// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// Why a candidate is not the same product as the release.
/// </summary>
public sealed record Mismatch(string Reason);

public readonly union MatchResult(ReleaseDetails, Mismatch);

/// <summary>
/// Decides whether a release of a provider is the same product as a release of YouTube Music.
/// </summary>
/// <remarks>
/// <para>
/// A candidate is the same product when all of these hold:
/// </para>
/// <list type="number">
/// <item><description>It has the same main artist, whatever a leading "the".</description></item>
/// <item><description>It has the same title once the edition notes are stripped.</description></item>
/// <item><description>It has as many tracks as the release.</description></item>
/// <item><description>Each available track of the release has the same title and the same explicit flag as the candidate track at its position, and a duration within <see cref="DurationTolerance"/>.</description></item>
/// </list>
/// <para>
/// Both catalogs number the tracks in flat order across the discs. A track without a duration on
/// either side fails, since nothing confirms it.
/// </para>
/// <para>
/// A match fills only the disc and ISRC data of each track. The provider data about the whole
/// release is not in a <see cref="CandidateRelease"/>, so the caller adds it.
/// </para>
/// </remarks>
public static class EnrichmentMatch
{
    internal static readonly TimeSpan DurationTolerance = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Compares the artist, the title and the track count, without looking at the tracks.
    /// </summary>
    /// <returns>The mismatch, or null when the candidate can still be the same product.</returns>
    public static Mismatch? CompareAlbum(Release release, CandidateRelease candidate)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(candidate);

        var artist = release.Artists.IsDefaultOrEmpty ? "" : release.Artists[0];
        if (
            !string.Equals(
                TitleText.Plain(candidate.Artist),
                TitleText.Plain(artist),
                StringComparison.Ordinal
            )
        )
        {
            return new Mismatch($"artist {candidate.Artist}");
        }

        if (
            !string.Equals(
                TitleText.Normalize(TitleText.StripEdition(candidate.Title)),
                TitleText.Normalize(TitleText.StripEdition(release.Title)),
                StringComparison.Ordinal
            )
        )
        {
            return new Mismatch($"title {candidate.Title}");
        }

        return candidate.TrackCount == release.TrackCount
            ? null
            : new Mismatch(
                string.Create(CultureInfo.InvariantCulture, $"{candidate.TrackCount} tracks")
            );
    }

    public static MatchResult Match(Release release, CandidateRelease candidate)
    {
        if (CompareAlbum(release, candidate) is { } album)
            return album;

        if (candidate.Tracks.IsDefaultOrEmpty || candidate.Tracks.Length != candidate.TrackCount)
            return new Mismatch("the track list differs from the track count");

        foreach (var track in release.Tracks)
        {
            if (track.Number < 1 || track.Number > candidate.Tracks.Length)
            {
                return new Mismatch(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"track {track.Number} is out of range"
                    )
                );
            }

            if (CompareTrack(track, candidate.Tracks[track.Number - 1]) is { } reason)
            {
                return new Mismatch(
                    string.Create(CultureInfo.InvariantCulture, $"track {track.Number} {reason}")
                );
            }
        }

        var discTrackCounts = candidate.Tracks.CountBy(track => track.Disc).ToImmutableDictionary();
        return new ReleaseDetails(
            Label: null,
            Barcode: null,
            ReleaseDate: null,
            ReleaseType: null,
            discTrackCounts.Keys.Max(),
            release.Tracks.ToImmutableDictionary(
                track => track.Number,
                track => Details(candidate.Tracks[track.Number - 1], discTrackCounts)
            )
        );
    }

    private static TrackDetails Details(
        CandidateTrack track,
        ImmutableDictionary<int, int> discTrackCounts
    ) => new(track.Isrc, track.Disc, track.NumberInDisc, discTrackCounts[track.Disc]);

    private static string? CompareTrack(ReleaseTrack track, CandidateTrack candidate)
    {
        if (
            !string.Equals(
                TitleText.Plain(track.Title),
                TitleText.Plain(candidate.Title),
                StringComparison.Ordinal
            )
        )
        {
            return $"title {candidate.Title}";
        }

        if (track.Explicit != candidate.Explicit)
            return "explicit flag";

        if (candidate.Duration is not { } offered)
            return "duration unknown";

        if (track.Duration is { } known && (known - offered).Duration() <= DurationTolerance)
            return null;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"duration {Durations.ToSeconds(offered)}s"
        );
    }
}
