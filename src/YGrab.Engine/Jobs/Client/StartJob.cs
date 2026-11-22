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
    /// <param name="Force">Grabs the link even when an earlier job already covers it.</param>
    /// <param name="Mode">Whether to grab the audio of a music link or one video.</param>
    /// <param name="Quality">The resolution cap of a video, which an audio job ignores.</param>
    public sealed record Command(
        string Link,
        bool Force = false,
        JobMode Mode = JobMode.Audio,
        VideoQuality Quality = VideoQuality.P1080
    );

    public sealed record Started(JobId Id);

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

            if (!Uri.TryCreate(command.Link.Trim(), UriKind.Absolute, out var url))
            {
                return new Invalid(
                    nameof(Command.Link),
                    JobRequest.InvalidLinkMessage(command.Mode)
                );
            }

            return JobRequest.Parse(url, command.Mode, command.Quality) switch
            {
                JobRequest request => await StartAsync(
                    url,
                    request,
                    command.Force,
                    cancellationToken
                ),
                ValidationError error => new Invalid(
                    error.Field ?? nameof(Command.Link),
                    error.Message
                ),
                null => throw Unreachable.NullUnion(),
            };
        }

        private async ValueTask<Outcome> StartAsync(
            Uri url,
            JobRequest request,
            bool force,
            CancellationToken cancellationToken
        )
        {
            var now = timeProvider.GetUtcNow();
            var job = Job.Create(new JobId(Guid.CreateVersion7(now)), url, request, force, now);
            if (jobs.FindCover(job) is { } previous)
                return new AlreadyGrabbed(previous);

            await jobs.AddAsync(job, cancellationToken);
            signal.Ring();
            return new Started(job.Id);
        }
    }
}
