// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.CommandLine;
using YGrab.Cli.Api;

namespace YGrab.Cli.Jobs;

internal sealed class WatchCommand : ApiCommand
{
    private readonly Argument<string?> _id = new("id")
    {
        Description = "The job id, or its last characters. Without one, every running job shows",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public WatchCommand(CliSession session)
        : base(session, "watch", "Follows a job, or every running job, until it ends") =>
        Arguments.Add(_id);

    protected override async Task<int> RunAsync(
        ApiClient api,
        ParseResult parse,
        CancellationToken cancellationToken
    )
    {
        var reporter = Reporter;
        if (parse.GetValue(_id) is not { } text)
            return await WatchAllAsync(api, cancellationToken);

        var found = await JobIds.FindAsync(api, text, cancellationToken);
        if (!found.TryGetValue(out var job, out var failure))
            return reporter.Report(failure);

        return await FollowProgress.UntilFinishedAsync(
            reporter,
            api,
            [job.Id],
            last => Task.FromResult(JobView.Conclude(reporter, last)),
            cancellationToken
        );
    }

    private async Task<int> WatchAllAsync(ApiClient api, CancellationToken cancellationToken)
    {
        try
        {
            var followed = await FollowProgress.RunAsync(
                Reporter.Output,
                api,
                watched: null,
                cancellationToken
            );
            return followed.TryGetValue(out _, out var failure)
                ? ExitCodes.Ok
                : Reporter.Report(failure);
        }
        catch (OperationCanceledException)
        {
            return ExitCodes.Ok;
        }
    }
}
