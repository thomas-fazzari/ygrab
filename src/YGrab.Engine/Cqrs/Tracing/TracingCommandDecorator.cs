// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine;

internal sealed class TracingCommandDecorator<TCommand, TOutcome>(
    ICommandHandler<TCommand, TOutcome> inner
) : ICommandHandler<TCommand, TOutcome>
{
    public ValueTask<TOutcome> HandleAsync(TCommand command, CancellationToken cancellationToken) =>
        HandlerSpans.TraceAsync(typeof(TCommand), () =>
            inner.HandleAsync(command, cancellationToken)
        );
}
