// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.Metrics;

namespace YGrab.Tests.Fakes;

/// <summary>
/// Creates real meters and never disposes them.
/// </summary>
internal sealed class FakeMeterFactory : IMeterFactory
{
    private FakeMeterFactory() { }

    public static FakeMeterFactory Instance { get; } = new();

    public Meter Create(MeterOptions options) => new(options);

    public void Dispose() { }
}
