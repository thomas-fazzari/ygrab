// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel.DataAnnotations;

namespace YGrab.Infrastructure.Configuration;

/// <summary>
/// The settings of yt-dlp.
/// </summary>
/// <remarks>
/// <para>
/// yt-dlp needs Deno on the <c>PATH</c> to solve the JavaScript challenges of YouTube.
/// </para>
/// <para>
/// A video download needs ffmpeg and ffprobe on the <c>PATH</c>, to merge the streams and to
/// embed the metadata, the chapters and the thumbnail.
/// </para>
/// <para>
/// A change applies to the next download, without a restart.
/// </para>
/// </remarks>
public sealed class YtDlpOptions
{
    public const string SECTION_NAME = "YtDlp";

    [Required]
    public string Executable { get; set; } = "yt-dlp";

    /// <summary>
    /// A Netscape cookies file, for when YouTube asks to sign in.
    /// </summary>
    public string? CookiesFile { get; set; }
}

[OptionsValidator]
internal sealed partial class YtDlpOptionsValidator : IValidateOptions<YtDlpOptions>;
