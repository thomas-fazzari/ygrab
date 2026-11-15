// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Globalization;
using YGrab.Cli.Api;
using YGrab.Contracts.Jobs;

namespace YGrab.Cli.Jobs;

/// <summary>
/// The CLI shows the last hex characters of an id, since the first ones of a UUIDv7 are a
/// timestamp that jobs added together share.
/// </summary>
internal static class JobIds
{
    internal const int ShortLength = 8;

    public static string Short(Guid id) => Hex(id)[^ShortLength..];

    public static async Task<ApiResult<JobDto>> FindAsync(
        ApiClient api,
        string text,
        CancellationToken cancellationToken
    )
    {
        var listed = await api.ListJobsAsync(limit: null, cancellationToken);
        return listed.TryGetValue(out var jobs, out var failure)
            ? Resolve(text, jobs)
            : ApiResult<JobDto>.Fail(failure);
    }

    public static ApiResult<JobDto> Resolve(string text, ImmutableArray<JobDto> jobs)
    {
        var suffix = text.Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        if (suffix.Length == 0 || !suffix.All(char.IsAsciiHexDigit))
        {
            return ApiResult<JobDto>.Fail(new ApiFailure.Unresolved($"'{text}' is not a job id."));
        }

        var matches = jobs.Where(job => Hex(job.Id).EndsWith(suffix, StringComparison.Ordinal))
            .ToList();
        return matches.Count switch
        {
            0 => ApiResult<JobDto>.Fail(new ApiFailure.Unresolved($"No job matches '{text}'.")),
            1 => ApiResult<JobDto>.Ok(matches[0]),
            _ => ApiResult<JobDto>.Fail(
                new ApiFailure.Unresolved($"'{text}' matches {matches.Count} jobs, add characters.")
            ),
        };
    }

    private static string Hex(Guid id) => id.ToString("N", CultureInfo.InvariantCulture);
}
