// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Tests.Fakes;

/// <summary>
/// A new directory under the temporary directory, deleted with its content on dispose.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public string FullName { get; } = Directory.CreateTempSubdirectory("ygrab-").FullName;

    public void Dispose()
    {
        if (Directory.Exists(FullName))
        {
            Directory.Delete(FullName, recursive: true);
        }
    }
}
