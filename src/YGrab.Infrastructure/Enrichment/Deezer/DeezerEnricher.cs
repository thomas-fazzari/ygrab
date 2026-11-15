// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using YGrab.Domain;
using YGrab.Engine;
using YGrab.Engine.Jobs;

namespace YGrab.Infrastructure.Deezer;

/// <summary>
/// Finds the release at Deezer and reads its label, barcode, release date, type and the ISRC of each track.
/// </summary>
/// <remarks>
/// <para>
/// The lookup searches the albums by artist and title, then by plain text. It keeps the albums that
/// <see cref="EnrichmentMatch"/> accepts, so the first one that is the same product wins.
/// </para>
/// <para>
/// Deezer sends its errors in a 200 body, which ends the lookup with
/// <see cref="EnrichmentFailure.ProviderError"/>. A transport failure, an unreadable body and the
/// time limit <see cref="EnrichTimeout"/> end it with <see cref="EnrichmentFailure.Unreachable"/>.
/// The lookup never throws except for a cancellation of the caller, and it never touches the
/// cooldown or the retries of the job, which belong to YouTube.
/// </para>
/// </remarks>
internal sealed partial class DeezerEnricher(
    IHttpClientFactory clients,
    TimeProvider timeProvider,
    ILogger<DeezerEnricher> logger
) : IReleaseEnricher
{
    internal const string ClientName = "deezer";
    internal const string BaseAddress = "https://api.deezer.com/";
    internal const string UserAgent = "YGrab";
    internal const int MaxResponseBytes = 1024 * 1024;

    internal static readonly TimeSpan EnrichTimeout = TimeSpan.FromSeconds(30);

    private const string SearchLimit = "10";
    private const int TrackLimit = 200;
    private const string ReleaseDateFormat = "yyyy-MM-dd";

    private const string AlbumRecord = "album";
    private const string EpRecord = "ep";
    private const string SingleRecord = "single";
    private const string CompilationRecord = "compile";

    public async ValueTask<Enrichment> EnrichAsync(
        Release release,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(release);

        using var limit = new CancellationTokenSource(EnrichTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            limit.Token
        );
        try
        {
            return await FindAsync(release, linked.Token);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogLookupFailed(exception);
            return new NotEnriched(EnrichmentFailure.Unreachable);
        }
    }

    private async ValueTask<Enrichment> FindAsync(
        Release release,
        CancellationToken cancellationToken
    )
    {
        if (release.Artists.IsDefaultOrEmpty)
        {
            return new NotEnriched(EnrichmentFailure.NoMatch);
        }

        var client = clients.CreateClient(ClientName);
        var artist = release.Artists[0];
        var title = TitleText.StripEdition(release.Title);
        string[] queries = [$"artist:\"{artist}\" album:\"{title}\"", $"{artist} {title}"];
        List<DeezerAlbumHit> hits = [];
        foreach (var query in queries)
        {
            var found = await GetAsync(
                client,
                SearchPath(query),
                DeezerJson.Default.DeezerSearchBody,
                cancellationToken
            );
            if (found.Error is { } error)
            {
                return Refused(error);
            }

            hits.AddRange(Items(found.Data));
        }

        foreach (var hit in hits.DistinctBy(hit => hit.Id))
        {
            if (await MatchAsync(client, release, hit, cancellationToken) is { } outcome)
            {
                return outcome;
            }
        }

        return new NotEnriched(EnrichmentFailure.NoMatch);
    }

    /// <returns>The details of a match, the failure of an error body, or null for another album.</returns>
    private async ValueTask<Enrichment?> MatchAsync(
        HttpClient client,
        Release release,
        DeezerAlbumHit hit,
        CancellationToken cancellationToken
    )
    {
        var artist = hit.Artist?.Name ?? "";
        var header = new CandidateRelease(hit.Title, artist, hit.NbTracks, []);
        if (EnrichmentMatch.CompareAlbum(release, header) is { } mismatch)
        {
            LogRejected(hit.Id, mismatch.Reason);
            return null;
        }

        var listed = await GetAsync(
            client,
            string.Create(
                CultureInfo.InvariantCulture,
                $"album/{hit.Id}/tracks?limit={TrackLimit}"
            ),
            DeezerJson.Default.DeezerTracksBody,
            cancellationToken
        );
        if (listed.Error is { } listError)
        {
            return Refused(listError);
        }

        var tracks = Items(listed.Data).Select(ToCandidate).ToImmutableArray();
        var candidate = new CandidateRelease(hit.Title, artist, tracks.Length, tracks);
        switch (EnrichmentMatch.Match(release, candidate))
        {
            case ReleaseDetails matched:
                var album = await GetAsync(
                    client,
                    string.Create(CultureInfo.InvariantCulture, $"album/{hit.Id}"),
                    DeezerJson.Default.DeezerAlbumBody,
                    cancellationToken
                );
                if (album.Error is { } albumError)
                {
                    return Refused(albumError);
                }

                return matched with
                {
                    Label = Present(album.Label),
                    Barcode = Present(album.Upc),
                    ReleaseDate = ToDate(album.ReleaseDate),
                    ReleaseType = ToType(album.RecordType),
                };
            case Mismatch rejected:
                LogRejected(hit.Id, rejected.Reason);
                return null;
            default:
                throw Unreachable.NullUnion();
        }
    }

    private static async ValueTask<T> GetAsync<T>(
        HttpClient client,
        string path,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken
    )
        where T : class
    {
        var bytes = await client.GetByteArrayAsync(
            new Uri(path, UriKind.Relative),
            cancellationToken
        );
        return JsonSerializer.Deserialize(bytes, typeInfo)
            ?? throw new JsonException("Deezer sent an empty body.");
    }

    private NotEnriched Refused(DeezerError error)
    {
        LogProviderError(error.Code, error.Message);
        return new NotEnriched(EnrichmentFailure.ProviderError);
    }

    private static string SearchPath(string query) =>
        $"search/album?q={Uri.EscapeDataString(query)}&limit={SearchLimit}";

    private static ImmutableArray<T> Items<T>(ImmutableArray<T> items) =>
        items.IsDefault ? [] : items;

    private static CandidateTrack ToCandidate(DeezerTrack track) =>
        new(
            track.Title,
            track.Duration,
            track.ExplicitLyrics,
            track.DiskNumber,
            track.TrackPosition,
            Present(track.Isrc)
        );

    private static string? Present(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static DateOnly? ToDate(string? text) =>
        DateOnly.TryParseExact(
            text,
            ReleaseDateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date
        )
            ? date
            : null;

    private static ReleaseType? ToType(string? recordType) =>
        recordType switch
        {
            AlbumRecord => ReleaseType.Album,
            EpRecord => ReleaseType.Ep,
            SingleRecord => ReleaseType.SingleRelease,
            CompilationRecord => ReleaseType.Compilation,
            _ => null,
        };

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Deezer could not be read, so the tracks keep the YouTube Music tags"
    )]
    private partial void LogLookupFailed(Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Deezer answered with the error {Code}: {Message}"
    )]
    private partial void LogProviderError(int code, string? message);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "The Deezer album {AlbumId} is not the same product: {Reason}"
    )]
    private partial void LogRejected(long albumId, string reason);
}
