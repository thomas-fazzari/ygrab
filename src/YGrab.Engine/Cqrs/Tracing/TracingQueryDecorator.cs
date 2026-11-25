// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine;

internal sealed class TracingQueryDecorator<TQuery, TResult>(IQueryHandler<TQuery, TResult> inner)
    : IQueryHandler<TQuery, TResult>
{
    public ValueTask<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken) =>
        HandlerSpans.TraceAsync(typeof(TQuery), () => inner.HandleAsync(query, cancellationToken));
}
