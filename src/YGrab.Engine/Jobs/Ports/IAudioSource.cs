// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Downloads the audio of a video into the files of a job, without converting it.
/// </summary>
public interface IAudioSource
{
    /// <param name="progress">Receives the downloaded fraction, from 0 to 1.</param>
    ValueTask<AudioDownload> DownloadAsync(
        JobId jobId,
        VideoId videoId,
        Action<double> progress,
        CancellationToken cancellationToken
    );

    /// <returns>True when an earlier run already kept the audio of the video.</returns>
    bool IsDownloaded(JobId jobId, VideoId videoId);
}

/// <summary>
/// The audio of the video is in the files of the job.
/// </summary>
public sealed record Downloaded;

public readonly union AudioDownload(Downloaded, RemoteFailure);
