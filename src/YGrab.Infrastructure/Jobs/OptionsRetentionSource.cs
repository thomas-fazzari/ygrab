// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;
using YGrab.Infrastructure.Configuration;

namespace YGrab.Infrastructure.Jobs;

internal sealed class OptionsRetentionSource(LastValidOptions<JobsOptions> options)
    : IRetentionSource
{
    public Retention Current => options.Current.Retention;
}
