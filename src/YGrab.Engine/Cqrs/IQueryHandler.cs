// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine;

public interface IQueryHandler<in TQuery, TResult>
{
    ValueTask<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
