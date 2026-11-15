// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// What a <see cref="Job"/> grabs: the audio of a music link, or one video.
/// </summary>
/// <remarks>
/// The record equality of a request is what dedupe compares.
/// </remarks>
public closed record JobRequest
{
    public sealed record Audio(MusicLink Link) : JobRequest;

    /// <param name="Quality">The resolution cap, which yt-dlp applies to the best format.</param>
    public sealed record Video(VideoId VideoId, VideoQuality Quality) : JobRequest;
}
