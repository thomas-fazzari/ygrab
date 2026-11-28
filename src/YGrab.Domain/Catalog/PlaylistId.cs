// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// The id of the audio playlist of an album, which starts with <see cref="PREFIX"/>.
/// </summary>
public sealed record PlaylistId
{
    internal const string PREFIX = "OLAK5uy_";

    private PlaylistId(string value) => Value = value;

    public string Value { get; }

    public static Result<PlaylistId> Create(string value) =>
        IdCharacters.Validate(value, "playlist id", PREFIX) is { } error
            ? error
            : new PlaylistId(value);
}
