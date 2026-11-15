// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine;

public static class Unreachable
{
    /// <summary>
    /// For the <c>null</c> arm that every switch over a <c>union</c> needs, since a default union
    /// is null.
    /// </summary>
    public static UnreachableException NullUnion() => new("A union value is never null.");
}
