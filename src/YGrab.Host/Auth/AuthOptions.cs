// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel.DataAnnotations;

namespace YGrab.Host.Auth;

/// <summary>
/// The secret of the API and the switch that turns the check off.
/// </summary>
/// <remarks>
/// <para>
/// A new token applies to the next request and signs every browser out. <see cref="Disabled"/>
/// is read at start.
/// </para>
/// <para>
/// The host refuses to start with <see cref="Disabled"/> set outside the Development environment.
/// </para>
/// <para>
/// Without a token, the host generates one at start and keeps it in a file of the data folder. A
/// start with <see cref="Disabled"/> set generates none.
/// </para>
/// </remarks>
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
