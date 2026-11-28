// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using YGrab.Contracts.Jobs;

namespace YGrab.Contracts;

/// <summary>
/// The names of the server-sent events, and the type of each payload.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><see cref="SNAPSHOT"/>: a <see cref="SnapshotDto"/>, the first event of each stream.</description></item>
/// <item><description><see cref="JOB"/>: a <see cref="JobDto"/>.</description></item>
/// <item><description><see cref="JOB_REMOVED"/>: a <see cref="JobRemovedDto"/>.</description></item>
/// <item><description><see cref="COOLDOWN"/>: a <see cref="CooldownDto"/>.</description></item>
/// </list>
/// </remarks>
public static class EventNames
{
    public const string SNAPSHOT = "snapshot";
    public const string JOB = "job";
    public const string JOB_REMOVED = "job-removed";
    public const string COOLDOWN = "cooldown";
}

public sealed record SnapshotDto(ImmutableArray<JobDto> Jobs, CooldownDto Cooldown);
