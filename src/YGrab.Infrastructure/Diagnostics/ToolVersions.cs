// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using YGrab.Engine.Diagnostics;
using YGrab.Infrastructure.Configuration;

namespace YGrab.Infrastructure.Diagnostics;

/// <summary>
/// Reads the versions of yt-dlp and ffmpeg from the programs themselves.
/// </summary>
/// <remarks>
/// <para>
/// Each read runs both programs again, since an update or a settings change replaces them while
/// the host runs.
/// </para>
/// </remarks>
internal sealed partial class ToolVersions(
    LastValidOptions<YtDlpOptions> options,
    TimeProvider timeProvider,
    ILogger<ToolVersions> logger
) : IComponentVersions
{
    internal static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    private const string FFMPEG_EXECUTABLE = "ffmpeg";
    private const string FFMPEG_PREFIX = "ffmpeg version ";

    public async ValueTask<IReadOnlyList<ComponentVersion>> ReadAsync(
        CancellationToken cancellationToken
    )
    {
        var ytDlp = ProbeAsync(options.Current.Executable, "--version", cancellationToken);
        var ffmpeg = ProbeAsync(FFMPEG_EXECUTABLE, "-version", cancellationToken);
        return
        [
            new ComponentVersion("yt-dlp", await ytDlp),
            new ComponentVersion("ffmpeg", FfmpegVersion(await ffmpeg)),
        ];
    }

    /// <returns>The first line of the output, or null when the program cannot run.</returns>
    private async Task<string?> ProbeAsync(
        string executable,
        string argument,
        CancellationToken cancellationToken
    )
    {
        var info = new ProcessStartInfo(executable, argument)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var timeoutSource = new CancellationTokenSource(ProbeTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token
        );
        Process process;
        try
        {
            process = Process.Start(info)!;
        }
        catch (Win32Exception exception)
        {
            LogProbeFailed(executable, exception.Message);
            return null;
        }

        using (process)
        {
            try
            {
                var output = await process.StandardOutput.ReadToEndAsync(linked.Token);
                await process.WaitForExitAsync(linked.Token);
                return process.ExitCode == 0 ? FirstLine(output) : null;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                LogProbeFailed(executable, $"no answer within {ProbeTimeout}");
                return null;
            }
            catch
            {
                process.Kill(entireProcessTree: true);
                throw;
            }
        }
    }

    private static string? FirstLine(string output)
    {
        var line = output.Trim().Split('\n', 2)[0].Trim();
        return string.IsNullOrWhiteSpace(line) ? null : line;
    }

    // ffmpeg starts with "ffmpeg version 7.1.1 Copyright (c) 2000-2025 the FFmpeg developers"
    private static string? FfmpegVersion(string? line) =>
        line?.StartsWith(FFMPEG_PREFIX, StringComparison.Ordinal) == true
            ? line[FFMPEG_PREFIX.Length..].Split(' ', 2)[0]
            : null;

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Cannot read the version of {Executable}: {Reason}"
    )]
    private partial void LogProbeFailed(string executable, string reason);
}
