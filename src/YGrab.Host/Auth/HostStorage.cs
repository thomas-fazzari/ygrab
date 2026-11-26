// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Host.Auth;

internal sealed record HostStorage(string DataDirectory)
{
    public string TokenFile => Path.Combine(DataDirectory, "auth-token");

    public string KeysDirectory => Path.Combine(DataDirectory, "keys");
}
