// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;

namespace YGrab.Tests.Fakes;

internal sealed class FakeJobOutput : IJobOutput
{
    public const string FileName = "album.zip";

    public List<Guid> Deleted { get; } = [];

    public HashSet<Guid> FailingDeletes { get; } = [];

    public List<ReleaseDetails?> Packaged { get; } = [];

    public ValueTask<string> PackageAsync(
        Job job,
        Cover? cover,
        ReleaseDetails? details,
        CancellationToken cancellationToken
    )
    {
        Packaged.Add(details);
        return ValueTask.FromResult(FileName);
    }

    public string? FindFile(Guid jobId, string fileName) => $"/jobs/{jobId:N}/{fileName}";

    public void Delete(Guid jobId)
    {
        if (FailingDeletes.Contains(jobId))
        {
            throw new IOException("The folder is locked.");
        }

        Deleted.Add(jobId);
    }
}
