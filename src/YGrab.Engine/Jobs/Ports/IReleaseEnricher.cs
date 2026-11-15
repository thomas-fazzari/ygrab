// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Looks up a release at a provider of extra tags: the label, the barcode, the ISRC of each track and more.
/// </summary>
public interface IReleaseEnricher
{
    /// <summary>
    /// Reads the details of a release from the provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A provider that is down, slow or without the release gives <see cref="NotEnriched"/>, so a
    /// lookup never fails the job.
    /// </para>
    /// <para>
    /// Only a cancellation of <paramref name="cancellationToken"/> throws.
    /// </para>
    /// </remarks>
    ValueTask<Enrichment> EnrichAsync(Release release, CancellationToken cancellationToken);
}

public readonly union Enrichment(ReleaseDetails, NotEnriched);

public sealed record NotEnriched(EnrichmentFailure Failure);
