// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine.Diagnostics;

/// <summary>
/// Reads the versions of the programs or packages that one adapter runs.
/// </summary>
public interface IComponentVersions
{
    /// <returns>One version per component, in a fixed order.</returns>
    ValueTask<IReadOnlyList<ComponentVersion>> ReadAsync(CancellationToken cancellationToken);
}

/// <param name="Version">The version, or null when it cannot be read.</param>
public sealed record ComponentVersion(string Name, string? Version);
