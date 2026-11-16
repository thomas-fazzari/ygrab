// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Contracts.Auth;

/// <summary>
/// A caller's session.
/// </summary>
/// <param name="AuthEnabled">Whether the host asks for a token. Without one, signing out does nothing.</param>
public sealed record SessionDto(bool AuthEnabled);
