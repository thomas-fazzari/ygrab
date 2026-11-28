// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// The UPC-A or EAN-13 barcode of a release.
/// </summary>
public sealed record Barcode
{
    private const int UPC_LENGTH = 12;
    private const int EAN_LENGTH = 13;

    private static readonly SearchValues<char> _digits = SearchValues.Create("0123456789");

    private Barcode(string value) => Value = value;

    public string Value { get; }

    public static Result<Barcode> Create(string value) =>
        IsValid(value)
            ? new Barcode(value)
            : new ValidationError(
                $"The barcode is not {UPC_LENGTH} or {EAN_LENGTH} digits from 0 to 9."
            );

    private static bool IsValid(string? value) =>
        value is { Length: UPC_LENGTH or EAN_LENGTH } && !value.AsSpan().ContainsAnyExcept(_digits);
}
