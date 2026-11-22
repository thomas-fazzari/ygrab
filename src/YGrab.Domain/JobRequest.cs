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
    private const string UnknownModeMessage = "The mode is not audio or video.";
    private const string UnknownQualityMessage = "The quality is not one of the video qualities.";

    public sealed record Audio(MusicLink Link) : JobRequest;

    /// <param name="Quality">The resolution cap, which yt-dlp applies to the best format.</param>
    public sealed record Video(VideoId VideoId, VideoQuality Quality) : JobRequest;

    public static Result<JobRequest> Parse(Uri url, JobMode mode, VideoQuality quality)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (!Enum.IsDefined(mode))
            return new ValidationError(UnknownModeMessage, nameof(mode));

        if (mode is JobMode.Video && !Enum.IsDefined(quality))
            return new ValidationError(UnknownQualityMessage, nameof(quality));

        return Find(url, mode, quality) is { } request
            ? new Result<JobRequest>(request)
            : new ValidationError(InvalidLinkMessage(mode));
    }

    public static string InvalidLinkMessage(JobMode mode) =>
        mode is JobMode.Video ? VideoLink.InvalidMessage : MusicLink.InvalidMessage;

    private static JobRequest? Find(Uri url, JobMode mode, VideoQuality quality) =>
        mode switch
        {
            JobMode.Video => VideoLink.Parse(url) is VideoId videoId
                ? new Video(videoId, quality)
                : null,
            _ => MusicLink.Parse(url) is MusicLink link ? new Audio(link) : null,
        };
}
