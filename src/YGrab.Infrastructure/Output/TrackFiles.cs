// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Infrastructure.Configuration;

namespace YGrab.Infrastructure.Output;

/// <summary>
/// The downloaded files of a job, one per video id in its tracks folder.
/// </summary>
/// <remarks>
/// A download moves there only once it is finished, so a file there is always complete.
/// </remarks>
internal static class TrackFiles
{
    extension(StorageOptions storage)
    {
        /// <returns>The path of the file of the video, or null when it is not downloaded.</returns>
        public string? FindTrackFile(Guid jobId, VideoId videoId)
        {
            var directory = storage.TracksDirectory(jobId);
            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, $"{videoId.Value}.*").FirstOrDefault()
                : null;
        }

        /// <summary>
        /// Moves a finished download into the tracks folder, named after its video id.
        /// </summary>
        public void KeepTrackFile(Guid jobId, VideoId videoId, string path)
        {
            var tracks = Directory.CreateDirectory(storage.TracksDirectory(jobId));
            File.Move(
                path,
                Path.Combine(tracks.FullName, videoId.Value + Path.GetExtension(path)),
                overwrite: true
            );
        }
    }
}
