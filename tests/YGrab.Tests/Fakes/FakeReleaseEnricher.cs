// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;

namespace YGrab.Tests.Fakes;

internal sealed class FakeReleaseEnricher : IReleaseEnricher
{
    public Enrichment Result { get; set; } = new NotEnriched(EnrichmentFailure.NoMatch);

    public List<Release> Lookups { get; } = [];

    public ValueTask<Enrichment> EnrichAsync(Release release, CancellationToken cancellationToken)
    {
        Lookups.Add(release);
        return ValueTask.FromResult(Result);
    }
}
