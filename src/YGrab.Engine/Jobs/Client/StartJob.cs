// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// Queues a job for a pasted link, in audio or video mode.
/// </summary>
/// <remarks>
/// <para>
/// The audio mode takes a YouTube Music album or track link, and the video mode takes a link to
/// one YouTube video.
/// </para>
/// <para>
/// The command has only flat fields, so it can be the body of a REST call.
/// </para>
/// </remarks>
public static class StartJob
{
    private const string UnknownModeMessage = "The mode is not audio or video.";
    private const string UnknownQualityMessage = "The quality is not one of the video qualities.";

    public enum JobMode
    {
        Audio = 0,
        Video = 1,
    }

    /// <param name="Force">Grabs the link even when an earlier job already covers it.</param>
    /// <param name="Mode">Whether to grab the audio of a music link or one video.</param>
    /// <param name="Quality">The resolution cap of a video, which an audio job ignores.</param>
    public sealed record Command(
        string Link,
        bool Force = false,
        JobMode Mode = JobMode.Audio,
        VideoQuality Quality = VideoQuality.P1080
    );

    public sealed record Started(Guid Id);

    public readonly union Outcome(Started, Invalid, AlreadyGrabbed);

    internal sealed class Handler(JobRegistry jobs, JobSignal signal, TimeProvider timeProvider)
        : ICommandHandler<Command, Outcome>
    {
        public async ValueTask<Outcome> HandleAsync(
            Command command,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(command);

            if (!Enum.IsDefined(command.Mode))
            {
                return new Invalid(nameof(Command.Mode), UnknownModeMessage);
            }

            if (command.Mode is JobMode.Video && !Enum.IsDefined(command.Quality))
            {
                return new Invalid(nameof(Command.Quality), UnknownQualityMessage);
            }

            if (
                !Uri.TryCreate(command.Link.Trim(), UriKind.Absolute, out var url)
                || ToRequest(command, url) is not { } request
            )
            {
                return new Invalid(nameof(Command.Link), InvalidMessage(command.Mode));
            }

            if (!command.Force && jobs.Find(job => job.Covers(request)) is { } previous)
            {
                return new AlreadyGrabbed(previous);
            }

            var now = timeProvider.GetUtcNow();
            var job = Job.Create(Guid.CreateVersion7(now), url, request, command.Force, now);
            await jobs.AddAsync(job, cancellationToken);
            signal.Ring();
            return new Started(job.Id);
        }

        private static JobRequest? ToRequest(Command command, Uri url) =>
            command.Mode switch
            {
                JobMode.Video => VideoLink.Parse(url) is VideoId videoId
                    ? new JobRequest.Video(videoId, command.Quality)
                    : null,
                _ => MusicLink.Parse(url) is MusicLink link ? new JobRequest.Audio(link) : null,
            };

        private static string InvalidMessage(JobMode mode) =>
            mode is JobMode.Video ? VideoLink.InvalidMessage : MusicLink.InvalidMessage;
    }
}
