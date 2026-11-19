// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// A failed validation.
/// </summary>
/// <param name="Field">The name of the input, when one factory validates several.</param>
public sealed record ValidationError(string Message, string? Field = null);

public readonly union Result<T>(T, ValidationError);
