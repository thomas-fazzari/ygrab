// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Host.Auth;

/// <summary>
/// The files that the host keeps in the data folder.
/// </summary>
internal sealed record HostStorage(string DataDirectory)
{
    public string TokenFile => Path.Combine(DataDirectory, "auth-token");

    /// <summary>
    /// The keys that protect the session cookies.
    /// </summary>
    public string KeysDirectory => Path.Combine(DataDirectory, "keys");
}
