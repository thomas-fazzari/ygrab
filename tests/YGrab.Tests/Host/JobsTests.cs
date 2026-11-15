// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Net.Mime;
using System.Text;
using YGrab.Contracts;
using YGrab.Contracts.Jobs;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Host;

public sealed class JobsTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_link_that_a_job_already_covers_answers_409_with_the_id_of_that_job()
    {
        await using var factory = new HostFactory();
        using var client = factory.CreateBearerClient();
        var request = new StartJobRequest(Samples.AlbumUrl);

        var first = await client.PostAsJsonAsync(
            HostFactory.JobsUrl,
            request,
            ApiJsonContext.Default.StartJobRequest,
            Cancellation
        );
        var second = await client.PostAsJsonAsync(
            HostFactory.JobsUrl,
            request,
            ApiJsonContext.Default.StartJobRequest,
            Cancellation
        );

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var started = await first.Content.ReadFromJsonAsync(
            ApiJsonContext.Default.StartedJobDto,
            Cancellation
        );
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await second.Content.ReadFromJsonAsync(
            ApiJsonContext.Default.ProblemBody,
            Cancellation
        );
        problem!.PreviousId.Should().Be(started!.Id);
    }

    [Theory]
    [InlineData("""{"mode":"audio"}""")]
    [InlineData("""{"link":"https://music.youtube.com/watch?v=abc","mode":7}""")]
    public async Task A_malformed_body_answers_400(string body)
    {
        await using var factory = new HostFactory();
        using var client = factory.CreateBearerClient();
        using var content = new StringContent(body, Encoding.UTF8, MediaTypeNames.Application.Json);

        var response = await client.PostAsync(HostFactory.JobsUrl, content, Cancellation);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
