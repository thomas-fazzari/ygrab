// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// A failed validation.
/// </summary>
/// <param name="Field">The name of the input, when one factory validates several.</param>
public sealed record ValidationError(string Message, string? Field = null);

[Union]
public readonly struct Result<T> : IUnion
{
    private const byte ValueTag = 1;
    private const byte ErrorTag = 2;

    private readonly T? _value;
    private readonly ValidationError? _error;
    private readonly byte _tag;

    public Result(T value)
    {
        _value = value;
        _tag = ValueTag;
    }

    public Result(ValidationError value)
    {
        _error = value;
        _tag = ErrorTag;
    }

    public object? Value =>
        _tag switch
        {
            ValueTag => _value,
            ErrorTag => _error,
            _ => null,
        };

    public bool HasValue => _tag != 0;

    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return _tag == ValueTag;
    }

    public bool TryGetValue([MaybeNullWhen(false)] out ValidationError value)
    {
        value = _error;
        return _tag == ErrorTag;
    }
}
