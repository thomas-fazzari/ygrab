// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Engine.Diagnostics;

namespace YGrab.Engine;

internal sealed class TracingQueryDecorator<TQuery, TResult>(IQueryHandler<TQuery, TResult> inner)
    : IQueryHandler<TQuery, TResult>
{
    private static readonly string _spanName = HandlerSpans.NameOf(typeof(TQuery));

    public ValueTask<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken) =>
        YGrabTelemetry.Source.HasListeners()
            ? HandlerSpans.TraceAsync(_spanName, () => inner.HandleAsync(query, cancellationToken))
            : inner.HandleAsync(query, cancellationToken);
}
