// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;

namespace YGrab.Tests.Fakes;

internal sealed class FakeJobOutput : IJobOutput
{
    public const string FileName = "album.zip";
    public const long FileSize = 1024;

    public List<JobId> Deleted { get; } = [];

    public HashSet<JobId> FailingDeletes { get; } = [];

    public List<ReleaseDetails?> Packaged { get; } = [];

    public ValueTask<PackagedFile> PackageAsync(
        Job job,
        Cover? cover,
        ReleaseDetails? details,
        CancellationToken cancellationToken
    )
    {
        Packaged.Add(details);
        return ValueTask.FromResult(new PackagedFile(FileName, FileSize));
    }

    public string? FindFile(JobId jobId, string fileName) => $"/jobs/{jobId.Value:N}/{fileName}";

    public void Delete(JobId jobId)
    {
        if (FailingDeletes.Contains(jobId))
            throw new IOException("The folder is locked.");

        Deleted.Add(jobId);
    }
}
