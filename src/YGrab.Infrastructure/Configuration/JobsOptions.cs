// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel.DataAnnotations;

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
    public const string SectionName = "Jobs";

    private const int MaxRetentionDays = 3650;

    /// <summary>
    /// The days that a finished job keeps its file.
    /// </summary>
    [Range(1, MaxRetentionDays)]
    public int RetentionDays { get; set; } = 7;

    /// <summary>
    /// The days that a finished video job keeps its file.
    /// </summary>
    [Range(1, MaxRetentionDays)]
    public int VideoRetentionDays { get; set; } = 2;

    internal TimeSpan Retention => TimeSpan.FromDays(RetentionDays);

    internal TimeSpan VideoRetention => TimeSpan.FromDays(VideoRetentionDays);
}

[OptionsValidator]
internal sealed partial class JobsOptionsValidator : IValidateOptions<JobsOptions>;
