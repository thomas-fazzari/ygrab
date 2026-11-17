// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;

namespace YGrab.Contracts.Diagnostics;

/// <summary>
/// The state of the server that the diagnostics show.
/// </summary>
/// <param name="StartedAt">When the server started.</param>
/// <param name="Components">The programs and packages that YGrab runs, in a fixed order.</param>
/// <param name="DataBytes">The size of the data folder.</param>
/// <param name="FreeBytes">The free space of the disk of the data folder.</param>
public sealed record StatusDto(
    DateTimeOffset StartedAt,
    ImmutableArray<ComponentDto> Components,
    long DataBytes,
    long FreeBytes
);

/// <param name="Version">The version, or null when the server cannot read it.</param>
public sealed record ComponentDto(string Name, string? Version);
