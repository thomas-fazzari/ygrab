// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Infrastructure.Configuration;

namespace YGrab.Infrastructure.YtDlp;

/// <summary>
/// Runs one yt-dlp process for one video and classifies its failure.
/// </summary>
/// <remarks>
/// <para>
/// Every yt-dlp call of YGrab goes through it, so the rate-limit arguments exist once. It adds
/// them to the arguments of the caller.
/// </para>
/// <para>
/// It empties the work folder before the run. yt-dlp writes its cookie jar back when it exits, so
/// it gets a copy of the cookies file in the work folder.
/// </para>
/// <para>
/// The process is killed when the run throws or the timeout ends.
/// </para>
/// <para>
/// A full disk throws an <see cref="IOException"/> and gives no failure, since a retry would
/// download the same data again and fail again.
/// </para>
/// </remarks>
internal sealed partial class YtDlpRunner(
    LastValidOptions<YtDlpOptions> options,
    TimeProvider timeProvider,
    ILogger<YtDlpRunner> logger
)
{
    private const string CookiesCopyName = "cookies.txt";
    private const string DiskFullMessage = "No space left on device";

    // The rate-limit arguments of yt-dlp: change them only for a reason that comes from YouTube
    private const string SleepRequestsSeconds = "1";
    private const string Retries = "10";
    private const string FragmentRetries = "10";
    private const string ExtractorRetries = "3";
    private const string RetrySleep = "exp=1:30";

    /// <param name="work">The work folder of the job, where the arguments write.</param>
    /// <param name="arguments">The arguments that follow the common ones.</param>
    /// <param name="onLine">Receives each line of the standard output.</param>
    /// <returns>Null when yt-dlp exits with 0, otherwise the classified failure.</returns>
    public async ValueTask<RemoteFailure?> RunAsync(
        string work,
        VideoId videoId,
        TimeSpan timeout,
        IReadOnlyList<string> arguments,
        Action<string> onLine,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(videoId);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(onLine);

        if (Directory.Exists(work))
        {
            Directory.Delete(work, recursive: true);
        }

        Directory.CreateDirectory(work);

        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token
        );
        using var process = Process.Start(StartInfo(work, videoId, arguments))!;
        var error = ReadErrorAsync(process.StandardError);
        try
        {
            while (await process.StandardOutput.ReadLineAsync(linked.Token) is { } line)
            {
                onLine(line);
            }

            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
            when (timeoutSource.IsCancellationRequested
                && !cancellationToken.IsCancellationRequested
            )
        {
            Stop(process);
            return new RemoteFailure.Transient(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The download took longer than {timeout.TotalMinutes:0} minutes."
                )
            );
        }
        catch
        {
            Stop(process);
            throw;
        }

        if (process.ExitCode == 0)
        {
            return null;
        }

        var lastError = await error;
        if (lastError?.Contains(DiskFullMessage, StringComparison.Ordinal) == true)
        {
            throw new IOException(lastError);
        }

        var failure = YtDlpErrors.Classify(lastError);
        LogRunFailed(videoId.Value, process.ExitCode, failure);
        return failure;
    }

    private ProcessStartInfo StartInfo(
        string directory,
        VideoId videoId,
        IReadOnlyList<string> arguments
    )
    {
        var settings = options.Current;
        var info = new ProcessStartInfo(settings.Executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        string[] common =
        [
            "--ignore-config",
            "--no-playlist",
            "--no-simulate",
            "--quiet",
            "--progress",
            "--newline",
            "--sleep-requests",
            SleepRequestsSeconds,
            "--retries",
            Retries,
            "--fragment-retries",
            FragmentRetries,
            "--extractor-retries",
            ExtractorRetries,
            "--retry-sleep",
            RetrySleep,
            "--retry-sleep",
            $"extractor:{RetrySleep}",
            "--retry-sleep",
            $"fragment:{RetrySleep}",
        ];
        foreach (var argument in common.Concat(arguments))
        {
            info.ArgumentList.Add(argument);
        }

        if (settings.CookiesFile is { } cookies)
        {
            var copy = Path.Combine(directory, CookiesCopyName);
            File.Copy(cookies, copy, overwrite: true);
            info.ArgumentList.Add("--cookies");
            info.ArgumentList.Add(copy);
        }

        info.ArgumentList.Add("--");
        info.ArgumentList.Add(videoId.Value);
        return info;
    }

    /// <summary>
    /// Splits an output line into the tag that a print template starts with and the rest.
    /// </summary>
    internal static (string Tag, string Value) Split(string line)
    {
        var space = line.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? (line, string.Empty) : (line[..space], line[(space + 1)..]);
    }

    // Waits so that no killed yt-dlp or ffmpeg still writes into the work folder
    private static void Stop(Process process)
    {
        process.Kill(entireProcessTree: true);
        process.WaitForExit();
    }

    /// <returns>The last error line of yt-dlp, without its prefix.</returns>
    private static async Task<string?> ReadErrorAsync(StreamReader reader)
    {
        const string errorPrefix = "ERROR: ";

        string? error = null;
        // The stream ends with the process, which the caller kills on any failure
        while (await reader.ReadLineAsync(CancellationToken.None) is { } line)
        {
            if (line.StartsWith(errorPrefix, StringComparison.Ordinal))
            {
                error = line[errorPrefix.Length..];
            }
        }

        return error;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "yt-dlp failed on {VideoId} with exit code {ExitCode}: {Failure}"
    )]
    private partial void LogRunFailed(string videoId, int exitCode, RemoteFailure failure);
}
