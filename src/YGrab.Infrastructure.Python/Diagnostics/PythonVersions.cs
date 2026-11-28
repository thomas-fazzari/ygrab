// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using CSnakes.Runtime;
using YGrab.Engine.Diagnostics;

namespace YGrab.Infrastructure.Python.Diagnostics;

/// <summary>
/// Reads the versions of Python and ytmusicapi, through <c>Python/versions.py</c>.
/// </summary>
internal sealed class PythonVersions(IPythonEnvironment python) : IComponentVersions
{
    private const string YT_MUSIC_API_PACKAGE = "ytmusicapi";

    // A call holds the GIL, so it runs off the caller's thread
    public async ValueTask<IReadOnlyList<ComponentVersion>> ReadAsync(
        CancellationToken cancellationToken
    ) => await Task.Run(Read, cancellationToken);

    private IReadOnlyList<ComponentVersion> Read()
    {
        var module = python.Versions();
        return
        [
            new ComponentVersion("Python", module.PythonVersion()),
            new ComponentVersion(YT_MUSIC_API_PACKAGE, module.PackageVersion(YT_MUSIC_API_PACKAGE)),
        ];
    }
}
