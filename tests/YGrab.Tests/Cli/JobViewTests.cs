// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Spectre.Console;
using YGrab.Cli.Jobs;
using YGrab.Contracts.Jobs;

namespace YGrab.Tests.Cli;

public sealed class JobViewTests
{
    [Fact]
    public void The_progress_line_of_a_job_with_brackets_and_tracks_is_valid_markup()
    {
        var job = Samples.JobDto(
            Guid.CreateVersion7(),
            "[Live] Album",
            currentTrack: 1,
            new TrackDto(1, "[Intro]", TrackStateCode.Pending, Reason: null)
        );

        var render = () => new Markup(JobView.Progress(job));

        render.Should().NotThrow();
    }
}
