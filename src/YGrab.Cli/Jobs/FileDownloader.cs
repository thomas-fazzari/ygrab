// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Buffers;
using Spectre.Console;
using YGrab.Cli.Api;
using YGrab.Contracts.Jobs;

namespace YGrab.Cli.Jobs;

internal static class FileDownloader
{
    private const int BufferSize = 81920;
    private const string PartialExtension = ".part";
    private const string FallbackName = "download";
    private const char InvalidReplacement = '_';

    private static readonly SearchValues<char> _invalid = SearchValues.Create(
        Path.GetInvalidFileNameChars()
    );

    /// <param name="output">A folder or a file path, or null for the current folder.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> SaveAsync(
        Reporter reporter,
        ApiClient api,
        JobDto job,
        string? output,
        CancellationToken cancellationToken
    )
    {
        var opened = await api.OpenFileAsync(job.Id, cancellationToken);
        if (!opened.TryGetValue(out var response, out var failure))
            return reporter.Report(failure);

        using (response)
        {
            var name = SafeName(NameOf(response) ?? job.FileName);
            var target = Target(output, name);
            var partial = target + PartialExtension;
            try
            {
                await CopyAsync(reporter.Output, response, name, partial, cancellationToken);
                File.Move(partial, target, overwrite: true);
            }
            catch (HttpIOException exception)
            {
                return reporter.Report(new ApiFailure.Unreachable(exception.Message));
            }
            catch (IOException exception)
            {
                return reporter.Fail($"Cannot write {target}: {exception.Message}");
            }
            finally
            {
                File.Delete(partial);
            }

            reporter.Output.WriteLine($"Saved {target}");
            return ExitCodes.Ok;
        }
    }

    private static string? NameOf(HttpResponseMessage response)
    {
        var disposition = response.Content.Headers.ContentDisposition;
        return (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"');
    }

    private static async Task CopyAsync(
        IAnsiConsole console,
        HttpResponseMessage response,
        string name,
        string partial,
        CancellationToken cancellationToken
    )
    {
        var length = response.Content.Headers.ContentLength;
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        await console
            .Progress()
            .Columns(
                new TaskDescriptionColumn { Alignment = Justify.Left },
                new ProgressBarColumn(),
                new PercentageColumn(),
                new TransferSpeedColumn()
            )
            .StartAsync(async context =>
            {
                var bar = context.AddTask(Markup.Escape(name), maxValue: length ?? 1);
                bar.IsIndeterminate = length is null;
                await using var file = new FileStream(
                    partial,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    BufferSize,
                    useAsync: true
                );
                var buffer = new byte[BufferSize];
                int read;
                while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    bar.Increment(read);
                }

                bar.StopTask();
            });
    }

    private static string SafeName(string? name)
    {
        var fileName = Path.GetFileName(name ?? string.Empty);
        var clean = string.Create(fileName.Length, fileName, static (span, source) =>
        {
            source.AsSpan().CopyTo(span);
            int index;
            while ((index = span.IndexOfAny(_invalid)) >= 0)
            {
                span[index] = InvalidReplacement;
            }
        });
        return string.IsNullOrWhiteSpace(clean) ? FallbackName : clean;
    }

    private static string Target(string? output, string name)
    {
        if (string.IsNullOrWhiteSpace(output))
            return Path.GetFullPath(name);

        var isFolder =
            Directory.Exists(output)
            || output.EndsWith(Path.DirectorySeparatorChar)
            || output.EndsWith(Path.AltDirectorySeparatorChar);
        if (!isFolder)
            return Path.GetFullPath(output);

        Directory.CreateDirectory(output);
        return Path.GetFullPath(Path.Combine(output, name));
    }
}
