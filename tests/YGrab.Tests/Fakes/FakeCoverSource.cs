// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Engine.Jobs;

namespace YGrab.Tests.Fakes;

internal sealed class FakeCoverSource : ICoverSource
{
    public ValueTask<Cover?> FetchAsync(Uri url, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Cover?>(null);
}
