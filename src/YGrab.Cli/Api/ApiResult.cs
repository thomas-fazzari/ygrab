// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using YGrab.Contracts;

namespace YGrab.Cli.Api;

internal closed record ApiFailure
{
    public sealed record Rejected(int Status, ProblemBody? Problem) : ApiFailure;

    public sealed record Unreachable(string Message) : ApiFailure;

    public sealed record BadToken : ApiFailure;

    public sealed record Unresolved(string Message) : ApiFailure;
}

internal readonly record struct None;

internal sealed record ApiResult<T>
{
    private readonly T? _value;
    private readonly ApiFailure? _failure;

    private ApiResult(T? value, ApiFailure? failure)
    {
        _value = value;
        _failure = failure;
    }

    public static ApiResult<T> Ok(T value) => new(value, failure: null);

    public static ApiResult<T> Fail(ApiFailure failure) => new(default, failure);

    public bool TryGetValue(
        [MaybeNullWhen(false)] out T value,
        [MaybeNullWhen(true)] out ApiFailure failure
    )
    {
        value = _value;
        failure = _failure;
        return _failure is null;
    }
}
