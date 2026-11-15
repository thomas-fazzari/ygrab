// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// The id of a YouTube Music album page, which starts with <see cref="Prefix"/>.
/// </summary>
public sealed record BrowseId : IStringValue<BrowseId>
{
    internal const string Prefix = "MPREb_";

    private BrowseId(string value) => Value = value;

    public string Value { get; }

    public static Result<BrowseId> Create(string value) =>
        IdCharacters.Validate(value, "browse id", Prefix) is { } error
            ? error
            : new BrowseId(value);
}
