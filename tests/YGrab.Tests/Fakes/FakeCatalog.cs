// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;

namespace YGrab.Tests.Fakes;

internal sealed class FakeCatalog(Release release) : ICatalog
{
    public int Lookups { get; private set; }

    public ValueTask<CatalogLookup> FindAsync(MusicLink link, CancellationToken cancellationToken)
    {
        Lookups++;
        return ValueTask.FromResult<CatalogLookup>(release);
    }
}
