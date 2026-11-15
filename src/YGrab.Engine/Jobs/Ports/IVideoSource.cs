// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Downloads one video into the files of a job, without transcoding it.
/// </summary>
/// <remarks>
/// The download keeps the video file and its <see cref="VideoInfo"/> together, so a kept video is
/// always complete.
/// </remarks>
public interface IVideoSource
{
    /// <param name="quality">The resolution cap, within which the best format wins.</param>
    /// <param name="progress">Receives the downloaded fraction, from 0 to 1.</param>
    ValueTask<VideoDownload> DownloadAsync(
        Guid jobId,
        VideoId videoId,
        VideoQuality quality,
        Action<double> progress,
        CancellationToken cancellationToken
    );

    /// <returns>The info of the video that an earlier run kept, or null.</returns>
    VideoInfo? FindKept(Guid jobId, VideoId videoId);
}

public readonly union VideoDownload(VideoInfo, RemoteFailure);
