// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Gives the retention of the finished jobs, which the settings can change while the host runs.
/// </summary>
public interface IRetentionSource
{
    Retention Current { get; }
}
