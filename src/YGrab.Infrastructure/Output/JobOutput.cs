// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO.Compression;
using YGrab.Domain;
using YGrab.Engine.Jobs;
using YGrab.Infrastructure.Configuration;

namespace YGrab.Infrastructure.Output;

/// <summary>
/// Builds the file of a job, with the tags that <see cref="TrackTagger"/> writes.
/// </summary>
/// <remarks>
/// <para>
/// The tags are written in place in the tracks folder, and the packaged file is written under a
/// temporary name and moved, so a run that a crash interrupted can run again.
/// </para>
/// <para>
/// The packaged file has a fixed name on disk. The name that the browser saves comes from the
/// release and is only kept in the job. The folder and the entry names inside a zip follow
/// <see cref="ReleaseNaming"/>.
/// </para>
/// <para>
/// A video job writes no tags, since yt-dlp embedded them. Its file moves from the tracks folder
/// to the output folder, and a repeated run finds it there.
/// </para>
/// <para>
/// Jobs run one at a time, which the global ATL settings need.
/// </para>
/// </remarks>
internal sealed class JobOutput(IOptions<StorageOptions> storage) : IJobOutput
{
    internal const string CoverFileName = "cover.jpg";

    private const string TemporaryExtension = ".tmp";
    private const string ZipExtension = ".zip";
    private const string AlbumFileBaseName = "package";
    private const string TrackFileBaseName = "track";

    public async ValueTask<PackagedFile> PackageAsync(
        Job job,
        Cover? cover,
        ReleaseDetails? details,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(job);

        var name = job.Request is JobRequest.Video video
            ? PackageVideo(job, video)
            : await PackageAudioAsync(job, cover, details, cancellationToken);
        return new PackagedFile(name, new FileInfo(PathOf(job.Id, name)).Length);
    }

    public string? FindFile(Guid jobId, string fileName)
    {
        var path = Path.GetFullPath(PathOf(jobId, fileName));
        return File.Exists(path) ? path : null;
    }

    public string PathOf(Guid jobId, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var baseName = string.Equals(extension, ZipExtension, StringComparison.Ordinal)
            ? AlbumFileBaseName
            : TrackFileBaseName;
        return Path.Combine(storage.Value.OutputDirectory(jobId), baseName + extension);
    }

    public void Delete(Guid jobId)
    {
        var directory = storage.Value.JobDirectory(jobId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private async ValueTask<string> PackageAudioAsync(
        Job job,
        Cover? cover,
        ReleaseDetails? details,
        CancellationToken cancellationToken
    )
    {
        var release = job.Release
            ?? throw new InvalidOperationException($"Job {job.Id} has no release to package.");
        var done = job.Tracks.Where(track => track.State is TrackState.Done).ToList();
        Directory.CreateDirectory(storage.Value.OutputDirectory(job.Id));

        var (name, build) = job.Request is JobRequest.Audio { Link: TrackLink }
            ? PackageSingle(job.Id, release, done[0].Track, cover, details)
            : PackageAlbum(job.Id, release, done, cover, details);
        var path = PathOf(job.Id, name);
        var temporary = path + TemporaryExtension;

        // A crash can leave a temporary file, which a zip cannot overwrite
        File.Delete(temporary);

        await build(temporary, cancellationToken);
        File.Move(temporary, path, overwrite: true);
        return name;
    }

    // The video is already tagged by yt-dlp, so a rename on the same file system is enough
    private string PackageVideo(Job job, JobRequest.Video request)
    {
        var info = job.KeptVideo
            ?? throw new InvalidOperationException($"Job {job.Id} has no kept video to package.");
        var output = Directory.CreateDirectory(storage.Value.OutputDirectory(job.Id)).FullName;
        var source = storage.Value.FindTrackFile(job.Id, request.VideoId);
        if (source is null)
        {
            // An earlier run moved the file before it crashed
            var packaged = Directory
                .EnumerateFiles(output, $"{TrackFileBaseName}.*")
                .FirstOrDefault()
                ?? throw new FileNotFoundException($"Job {job.Id} has no video file.");
            return ReleaseNaming.VideoName(request.VideoId, info, Path.GetExtension(packaged));
        }

        var extension = Path.GetExtension(source);
        File.Move(source, Path.Combine(output, TrackFileBaseName + extension), overwrite: true);
        return ReleaseNaming.VideoName(request.VideoId, info, extension);
    }

    private (string Name, Func<string, CancellationToken, ValueTask> Build) PackageSingle(
        Guid jobId,
        Release release,
        ReleaseTrack track,
        Cover? cover,
        ReleaseDetails? details
    )
    {
        var source = TrackFile(jobId, track);
        return (ReleaseNaming.SingleName(track, Path.GetExtension(source)), (temporary, _) =>
        {
            TrackTagger.Write(source, release, track, cover, details);
            File.Copy(source, temporary, overwrite: true);
            return ValueTask.CompletedTask;
        });
    }

    private (string Name, Func<string, CancellationToken, ValueTask> Build) PackageAlbum(
        Guid jobId,
        Release release,
        List<JobTrack> done,
        Cover? cover,
        ReleaseDetails? details
    )
    {
        var folder = ReleaseNaming.AlbumName(release);
        return (folder + ZipExtension, async (temporary, cancellationToken) =>
        {
            await using var zip = await ZipFile.OpenAsync(
                temporary,
                ZipArchiveMode.Create,
                cancellationToken
            );
            foreach (var jobTrack in done)
            {
                var source = TrackFile(jobId, jobTrack.Track);
                TrackTagger.Write(source, release, jobTrack.Track, cover, details);
                var entry =
                    $"{folder}/{ReleaseNaming.EntryName(jobTrack.Track, Path.GetExtension(source))}";
                // Audio streams are already compressed
                await zip.CreateEntryFromFileAsync(
                    source,
                    entry,
                    CompressionLevel.NoCompression,
                    cancellationToken
                );
            }

            if (cover is not null)
            {
                var entry = zip.CreateEntry(
                    $"{folder}/{CoverFileName}",
                    CompressionLevel.NoCompression
                );
                await using var stream = await entry.OpenAsync(cancellationToken);
                await stream.WriteAsync(cover.Jpeg, cancellationToken);
            }
        });
    }

    private string TrackFile(Guid jobId, ReleaseTrack track) =>
        storage.Value.FindTrackFile(jobId, track.VideoId)
        ?? throw new FileNotFoundException($"Job {jobId} has no file for track {track.Number}.");
}
