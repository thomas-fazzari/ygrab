// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Contracts;

/// <summary>
/// The pause after YouTube rate-limits the host.
/// </summary>
/// <param name="Until">When the pause ends, or null when none runs.</param>
public sealed record CooldownDto(int Strikes, DateTimeOffset? Until, string? Reason);
