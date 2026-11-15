// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

public interface IStringValue<TSelf>
    where TSelf : IStringValue<TSelf>
{
    static abstract Result<TSelf> Create(string value);

    string Value { get; }
}
