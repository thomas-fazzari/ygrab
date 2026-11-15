// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

public sealed record VideoId : IStringValue<VideoId>
{
    private VideoId(string value) => Value = value;

    public string Value { get; }

    public static Result<VideoId> Create(string value) =>
        IdCharacters.Validate(value, "video id") is { } error ? error : new VideoId(value);
}
