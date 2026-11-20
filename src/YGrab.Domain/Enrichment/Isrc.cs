// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// The International Standard Recording Code of a recording.
/// </summary>
public sealed record Isrc
{
    private const int Length = 12;

    private static readonly SearchValues<char> _allowed = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
    );

    private Isrc(string value) => Value = value;

    public string Value { get; }

    public static Result<Isrc> Create(string value) =>
        IsValid(value)
            ? new Isrc(value)
            : new ValidationError($"The ISRC is not {Length} characters from A-Z and 0-9.");

    private static bool IsValid(string? value) =>
        value is { Length: Length } && !value.AsSpan().ContainsAnyExcept(_allowed);
}
