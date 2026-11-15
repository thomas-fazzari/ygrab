// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Cli.Api;
using YGrab.Cli.Jobs;

namespace YGrab.Tests.Cli;

public sealed class JobIdsTests
{
    private static readonly Guid _first = Guid.Parse("019e0000-0000-7000-8000-00000000abcd");
    private static readonly Guid _second = Guid.Parse("019e0000-0000-7000-8000-00000000ffcd");

    [Fact]
    public void A_suffix_picks_one_job_and_a_shared_suffix_is_refused()
    {
        var jobs = ImmutableArray.Create(Samples.JobDto(_first), Samples.JobDto(_second));

        var unique = JobIds.Resolve("abcd", jobs);
        var shared = JobIds.Resolve("cd", jobs);
        var unknown = JobIds.Resolve("beef", jobs);
        var notHex = JobIds.Resolve("xyz", jobs);

        unique.TryGetValue(out var found, out _).Should().BeTrue();
        found!.Id.Should().Be(_first);
        foreach (var refused in new[] { shared, unknown, notHex })
        {
            refused.TryGetValue(out _, out var failure).Should().BeFalse();
            failure.Should().BeOfType<ApiFailure.Unresolved>();
        }
    }
}
