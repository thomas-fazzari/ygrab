// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Engine.Diagnostics;

namespace YGrab.Engine;

internal sealed class TracingDecorator<TRequest, TResponse>(
    IRequestHandler<TRequest, TResponse> inner
) : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly string _spanName = HandlerSpans.NameOf(typeof(TRequest));

    public ValueTask<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken
    ) =>
        YGrabTelemetry.Source.HasListeners()
            ? HandlerSpans.TraceAsync(_spanName, () =>
                inner.HandleAsync(request, cancellationToken)
            )
            : inner.HandleAsync(request, cancellationToken);
}
