// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine;

public interface ICommandHandler<in TCommand, TOutcome>
{
    ValueTask<TOutcome> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
