// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Engine;

public interface IRequest<out TResponse>;

public interface ICommand<out TOutcome> : IRequest<TOutcome>;

public interface IQuery<out TResult> : IRequest<TResult>;

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
