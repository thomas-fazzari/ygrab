// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel.DataAnnotations;

namespace YGrab.Host.Auth;

/// <summary>
/// A new token applies from the next request, while <see cref="Disabled"/> is read at start.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    internal const int MinTokenLength = 16;

    [MinLength(MinTokenLength)]
    public string? Token
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public bool Disabled { get; set; }
}

[OptionsValidator]
internal sealed partial class AuthOptionsValidator : IValidateOptions<AuthOptions>;
