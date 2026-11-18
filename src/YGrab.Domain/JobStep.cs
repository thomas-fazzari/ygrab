// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// The work that a resolving or downloading <see cref="Job"/> needs next.
/// </summary>
public closed record JobStep
{
    public sealed record Resolve(MusicLink Link) : JobStep;

    public sealed record DownloadTrack(JobTrack Track) : JobStep;

    public sealed record DownloadVideo(JobRequest.Video Video) : JobStep;

    public sealed record Package : JobStep;
}
