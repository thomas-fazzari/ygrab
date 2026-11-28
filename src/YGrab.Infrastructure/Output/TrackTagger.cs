// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using ATL;
using YGrab.Domain;
using YGrab.Engine.Jobs;

namespace YGrab.Infrastructure.Output;

/// <summary>
/// Writes the tags of one audio file with ATL, following the keys of MusicBrainz Picard.
/// </summary>
/// <remarks>
/// <para>
/// A run replaces every tag that it owns, so a file that a crashed run tagged with provider data
/// loses it when the next run has none. The keys differ by container:
/// </para>
/// <list type="bullet">
/// <item><description>Opus holds Vorbis comments, and ATL maps the ISRC of a track to <c>ISRC</c>. <c>ARTISTS</c>, <c>ALBUMARTISTS</c> and the type of a compilation hold one field per value.</description></item>
/// <item><description>M4A holds MP4 atoms. <see cref="Track.ISRC"/> writes the atom <c>©isr</c>, which Picard and Navidrome ignore, so the ISRC goes to a <c>com.apple.iTunes</c> atom. ATL writes one value per atom, so M4A has no artist lists and a compilation is typed <c>compilation</c> only.</description></item>
/// </list>
/// <para>
/// <c>ARTIST</c> and <c>ALBUMARTIST</c> hold the credit that <see cref="ReleaseNaming.Credit"/> builds, as Picard does.
/// </para>
/// <para>
/// ATL has three pitfalls here. <see cref="Track.OriginalReleaseDate"/> writes <c>ORIGINALDATE </c>
/// with a trailing space in Vorbis, so the original date goes through the additional fields.
/// <see cref="Track.Year"/> and <see cref="Track.Date"/> share one field. The MP4 atom <c>rtng</c>
/// also holds <see cref="Track.Popularity"/>, which replaces the explicit flag unless it is null.
/// </para>
/// </remarks>
internal static class TrackTagger
{
    private const string OPUS_EXTENSION = ".opus";
    private const string M4A_EXTENSION = ".m4a";

    private const string ORIGINAL_DATE_KEY = "ORIGINALDATE";
    private const string ORIGINAL_YEAR_KEY = "ORIGINALYEAR";
    private const string LABEL_KEY = "LABEL";
    private const string BARCODE_KEY = "BARCODE";
    private const string RELEASE_TYPE_KEY = "RELEASETYPE";
    private const string ADVISORY_KEY = "ITUNESADVISORY";
    private const string ARTISTS_KEY = "ARTISTS";
    private const string ALBUM_ARTISTS_KEY = "ALBUMARTISTS";
    private const string MP4_RELEASE_TYPE_KEY = "MusicBrainz Album Type";
    private const string MP4_ISRC_KEY = "ISRC";
    private const string MP4_ISRC_ATOM_KEY = "----:com.apple.iTunes:ISRC";
    private const string MP4_ADVISORY_KEY = "rtng";
    private const string EXPLICIT_VALUE = "1";

    private const string ALBUM_TYPE = "album";
    private const string EP_TYPE = "ep";
    private const string SINGLE_TYPE = "single";
    private const string COMPILATION_TYPE = "compilation";

    private static readonly string[] _opusKeys =
    [
        ORIGINAL_DATE_KEY,
        ORIGINAL_YEAR_KEY,
        LABEL_KEY,
        BARCODE_KEY,
        RELEASE_TYPE_KEY,
        ADVISORY_KEY,
        ARTISTS_KEY,
        ALBUM_ARTISTS_KEY,
    ];

    private static readonly string[] _m4aKeys =
    [
        ORIGINAL_DATE_KEY,
        LABEL_KEY,
        BARCODE_KEY,
        MP4_RELEASE_TYPE_KEY,
        MP4_ISRC_KEY,
        MP4_ISRC_ATOM_KEY,
        MP4_ADVISORY_KEY,
    ];

    /// <param name="details">What the provider knows about the release, or null for the tags of YouTube Music only.</param>
    public static void Write(
        string path,
        Release release,
        ReleaseTrack source,
        Cover? cover,
        ReleaseDetails? details
    )
    {
        var found = details?.Tracks.GetValueOrDefault(source.Number);
        var track = new Track(path)
        {
            Title = source.Title,
            Artist = ReleaseNaming.Credit(source.Artists),
            AlbumArtist = ReleaseNaming.Credit(release.Artists),
            Album = release.Title,
        };
        WriteNumbers(track, release, source, details, found);
        WriteDate(track, release, details);
        track.EmbeddedPictures.Clear();
        if (cover is not null)
        {
            track.EmbeddedPictures.Add(
                PictureInfo.fromBinaryData(cover.Jpeg.ToArray(), PictureInfo.PIC_TYPE.Front)
            );
        }

        var extras = found is null ? null : details;
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case OPUS_EXTENSION:
                WriteVorbis(track, release, source, extras, found);
                break;
            case M4A_EXTENSION:
                WriteMp4(track, release, source, extras, found);
                break;
        }

        if (!track.Save())
            throw new IOException($"ATL could not write the tags of {path}.");
    }

    private static void WriteNumbers(
        Track track,
        Release release,
        ReleaseTrack source,
        ReleaseDetails? details,
        TrackDetails? found
    )
    {
        if (details is null || found is null)
        {
            track.TrackNumber = source.Number;
            track.TrackTotal = release.TrackCount;
            track.DiscNumber = null;
            track.DiscTotal = null;
            return;
        }

        track.TrackNumber = found.NumberInDisc;
        track.TrackTotal = found.DiscTrackCount;
        track.DiscNumber = found.Disc;
        track.DiscTotal = details.DiscTotal;
    }

    // Year and Date share one field, so the year is set after the date is cleared
    private static void WriteDate(Track track, Release release, ReleaseDetails? details)
    {
        track.Date = null;
        track.Year = null;
        if (details?.ReleaseDate is { } date)
        {
            track.Date = date.ToDateTime(TimeOnly.MinValue);
            return;
        }

        track.Year = release.Year;
    }

    private static void WriteVorbis(
        Track track,
        Release release,
        ReleaseTrack source,
        ReleaseDetails? details,
        TrackDetails? found
    )
    {
        var fields = track.AdditionalFields;
        Clear(fields, _opusKeys);

        // ATL keeps the old ISRC when it is set to null, and an empty text clears it
        track.ISRC = found?.Isrc?.Value ?? string.Empty;

        Put(fields, ARTISTS_KEY, Values(source.Artists));
        Put(fields, ALBUM_ARTISTS_KEY, Values(release.Artists));
        if (details is not null)
        {
            if (release.Year is { } year)
            {
                var text = YearText(year);
                fields[ORIGINAL_DATE_KEY] = text;
                fields[ORIGINAL_YEAR_KEY] = text;
            }

            Put(fields, LABEL_KEY, details.Label);
            Put(fields, BARCODE_KEY, details.Barcode?.Value);
            Put(
                fields,
                RELEASE_TYPE_KEY,
                ReleaseTypeText(details.ReleaseType, Values([ALBUM_TYPE, COMPILATION_TYPE]))
            );
        }

        if (source.Explicit)
            fields[ADVISORY_KEY] = EXPLICIT_VALUE;
    }

    private static void WriteMp4(
        Track track,
        Release release,
        ReleaseTrack source,
        ReleaseDetails? details,
        TrackDetails? found
    )
    {
        var fields = track.AdditionalFields;
        Clear(fields, _m4aKeys);
        track.Popularity = null;
        Put(fields, MP4_ISRC_ATOM_KEY, found?.Isrc?.Value);
        if (details is not null)
        {
            if (release.Year is { } year)
                fields[ORIGINAL_DATE_KEY] = YearText(year);

            Put(fields, LABEL_KEY, details.Label);
            Put(fields, BARCODE_KEY, details.Barcode?.Value);
            Put(
                fields,
                MP4_RELEASE_TYPE_KEY,
                ReleaseTypeText(details.ReleaseType, COMPILATION_TYPE)
            );
        }

        if (source.Explicit)
            fields[MP4_ADVISORY_KEY] = EXPLICIT_VALUE;
    }

    private static void Clear(IDictionary<string, string> fields, string[] keys)
    {
        foreach (var key in keys)
        {
            fields.Remove(key);
        }
    }

    private static void Put(IDictionary<string, string> fields, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            fields[key] = value;
    }

    private static string YearText(int year) => year.ToString(CultureInfo.InvariantCulture);

    private static string? ReleaseTypeText(ReleaseType? type, string compilation) =>
        type switch
        {
            null => null,
            ReleaseType.Album => ALBUM_TYPE,
            ReleaseType.Ep => EP_TYPE,
            ReleaseType.SingleRelease => SINGLE_TYPE,
            ReleaseType.Compilation => compilation,
            _ => throw new InvalidOperationException("A release has an unknown type."),
        };

    // ATL writes the values between its display separators as separate Vorbis fields
    private static string Values(ImmutableArray<string> values) =>
        string.Join(Settings.DisplayValueSeparator, values);
}
