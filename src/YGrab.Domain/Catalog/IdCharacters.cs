// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

internal static class IdCharacters
{
    private static readonly SearchValues<char> _allowed = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"
    );

    /// <returns>The error, or null when <paramref name="value"/> is a valid id.</returns>
    internal static ValidationError? Validate(string? value, string name, string? prefix = null)
    {
        if (string.IsNullOrWhiteSpace(value) || value.AsSpan().ContainsAnyExcept(_allowed))
        {
            return new ValidationError(
                $"The {name} is empty or has a character outside A-Z, a-z, 0-9, - and _."
            );
        }

        return prefix is not null && !value.StartsWith(prefix, StringComparison.Ordinal)
            ? new ValidationError($"The {name} does not start with {prefix}.")
            : null;
    }
}
