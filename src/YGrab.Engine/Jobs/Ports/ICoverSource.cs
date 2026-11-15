// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine.Jobs;

public interface ICoverSource
{
    /// <returns>The JPEG cover, or null when it cannot be fetched.</returns>
    ValueTask<Cover?> FetchAsync(Uri url, CancellationToken cancellationToken);
}

public sealed record Cover(ReadOnlyMemory<byte> Jpeg);
