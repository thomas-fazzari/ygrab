// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel.DataAnnotations;
using YGrab.Domain;

namespace YGrab.Infrastructure.Configuration;

/// <summary>
/// The lifetime of the finished jobs.
/// </summary>
/// <remarks>
/// <para>
/// A video job has its own retention, since its file is far bigger than an audio file.
/// </para>
/// <para>
/// A change applies at the next sweep, without a restart.
/// </para>
/// </remarks>
public sealed class JobsOptions
{
    public const string SECTION_NAME = "Jobs";

    private const int MAX_RETENTION_DAYS = 3650;

    /// <summary>
    /// The days that a finished job keeps its file.
    /// </summary>
    [Range(1, MAX_RETENTION_DAYS)]
    public int RetentionDays { get; set; } = 7;

    /// <summary>
    /// The days that a finished video job keeps its file.
    /// </summary>
    [Range(1, MAX_RETENTION_DAYS)]
    public int VideoRetentionDays { get; set; } = 2;

    /// <summary>
    /// The days that a finished job stays in the history, after which the sweep removes it.
    /// </summary>
    [Range(1, MAX_RETENTION_DAYS)]
    public int HistoryDays { get; set; } = 90;

    internal Retention Retention =>
        new(
            TimeSpan.FromDays(RetentionDays),
            TimeSpan.FromDays(VideoRetentionDays),
            TimeSpan.FromDays(HistoryDays)
        );
}

[OptionsValidator]
internal sealed partial class JobsOptionsValidator : IValidateOptions<JobsOptions>;
