// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.CommandLine;
using System.CommandLine.Parsing;
using Spectre.Console;
using YGrab.Cli.Api;
using YGrab.Contracts.Jobs;

namespace YGrab.Cli.Jobs;

internal sealed class AddCommand : ApiCommand
{
    private const string StandardInput = "-";

    private readonly Argument<string[]> _links = new("links")
    {
        Description = $"The links to grab, or {StandardInput} to read them from the standard input",
        Arity = ArgumentArity.OneOrMore,
    };

    private readonly Option<bool> _video = new("--video")
    {
        Description = "Grab the video of each link instead of the audio",
    };

    private readonly Option<VideoQualityCode?> _quality = new("--quality", "-q")
    {
        Description = "The resolution cap of a video, or audio to keep its sound only",
        HelpName = string.Join('|', Qualities.Names),
        CustomParser = ParseQuality,
    };

    private readonly Option<bool> _force = new("--force", "-f")
    {
        Description = "Grab a link even when an earlier job already covers it",
    };

    private readonly Option<bool> _wait = new("--wait", "-w")
    {
        Description = "Follow the jobs, then download each file to the current folder",
    };

    public AddCommand(CliSession session)
        : base(session, "add", "Queues links to grab")
    {
        _quality.CompletionSources.Add(Qualities.Names);
        Arguments.Add(_links);
        Options.Add(_video);
        Options.Add(_quality);
        Options.Add(_force);
        Options.Add(_wait);
    }

    protected override async Task<int> RunAsync(
        ApiClient api,
        ParseResult parse,
        CancellationToken cancellationToken
    )
    {
        var links = await ReadLinksAsync(parse.GetRequiredValue(_links), cancellationToken);
        var wait = parse.GetValue(_wait);
        var started = new List<Guid>();
        var exit = ExitCodes.Ok;
        foreach (var link in links)
        {
            var request = new StartJobRequest(
                link,
                parse.GetValue(_video) ? JobModeCode.Video : JobModeCode.Audio,
                Force: parse.GetValue(_force)
            );
            if (parse.GetValue(_quality) is { } quality)
                request = request with { Quality = quality };

            var result = await api.StartJobAsync(request, cancellationToken);
            if (result.TryGetValue(out var job, out var failure))
            {
                Reporter.Output.WriteLine($"{JobIds.Short(job.Id)} queued {link}");
                started.Add(job.Id);
                continue;
            }

            if (wait && failure is ApiFailure.Rejected { Problem.PreviousId: { } previous })
            {
                Reporter.Output.WriteLine(
                    $"{link} is already grabbed, following {JobIds.Short(previous)}"
                );
                started.Add(previous);
                continue;
            }

            exit = Math.Max(exit, Reporter.Report(failure));
            if (exit == ExitCodes.Unreachable)
                return exit;
        }

        if (!wait || started.Count == 0)
            return exit;

        var followed = await FollowProgress.UntilFinishedAsync(
            Reporter,
            api,
            started,
            job => ConcludeAsync(api, job, cancellationToken),
            cancellationToken
        );
        return Math.Max(exit, followed);
    }

    private async Task<int> ConcludeAsync(
        ApiClient api,
        JobDto job,
        CancellationToken cancellationToken
    )
    {
        var exit = JobView.Conclude(Reporter, job);
        if (job.FileName is null)
            return exit;

        var saved = await FileDownloader.SaveAsync(
            Reporter,
            api,
            job,
            output: null,
            cancellationToken
        );
        return Math.Max(exit, saved);
    }

    private static async Task<List<string>> ReadLinksAsync(
        string[] arguments,
        CancellationToken cancellationToken
    )
    {
        var links = new List<string>();
        foreach (var argument in arguments)
        {
            if (!string.Equals(argument, StandardInput, StringComparison.Ordinal))
            {
                links.Add(argument);
                continue;
            }

            while (await Console.In.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    links.Add(line.Trim());
            }
        }

        return links;
    }

    private static VideoQualityCode? ParseQuality(ArgumentResult result)
    {
        var name = result.Tokens[0].Value;
        if (Qualities.Parse(name) is { } quality)
            return quality;

        result.AddError(
            $"Unknown quality {name}. Use one of {string.Join(", ", Qualities.Names)}."
        );
        return null;
    }
}
