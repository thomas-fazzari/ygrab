// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// What yt-dlp reports about a downloaded video.
/// </summary>
public sealed record VideoInfo(string Title, string? Channel);
