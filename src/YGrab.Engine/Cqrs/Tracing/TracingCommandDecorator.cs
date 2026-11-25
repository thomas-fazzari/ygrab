// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Engine.Diagnostics;

namespace YGrab.Engine;

internal sealed class TracingCommandDecorator<TCommand, TOutcome>(
    ICommandHandler<TCommand, TOutcome> inner
) : ICommandHandler<TCommand, TOutcome>
{
    private static readonly string _spanName = HandlerSpans.NameOf(typeof(TCommand));

    public ValueTask<TOutcome> HandleAsync(TCommand command, CancellationToken cancellationToken) =>
        YGrabTelemetry.Source.HasListeners()
            ? HandlerSpans.TraceAsync(_spanName, () =>
                inner.HandleAsync(command, cancellationToken)
            )
            : inner.HandleAsync(command, cancellationToken);
}
