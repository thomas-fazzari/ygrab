// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine.Diagnostics;

public interface IServerUsage
{
    ServerUsage Read();
}

/// <param name="StartedAt">When the server process started.</param>
/// <param name="DataBytes">The size of the data folder.</param>
/// <param name="FreeBytes">The free space of its disk.</param>
public sealed record ServerUsage(DateTimeOffset StartedAt, long DataBytes, long FreeBytes);
