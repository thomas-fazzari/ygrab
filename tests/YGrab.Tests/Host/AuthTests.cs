// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YGrab.Contracts;
using YGrab.Contracts.Auth;
using YGrab.Host.Auth;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Host;

public sealed class AuthTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_api_refuses_a_request_without_the_token_and_accepts_the_bearer_token()
    {
        await using var factory = new HostFactory();
        using var anonymous = factory.CreateClient();
        using var wrong = factory.CreateClient();
        wrong.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            BearerHandler.SchemeName,
            "wrong"
        );
        using var bearer = factory.CreateBearerClient();

        var refused = await anonymous.GetAsync(HostFactory.JobsUrl, Cancellation);
        var mismatch = await wrong.GetAsync(HostFactory.JobsUrl, Cancellation);
        var accepted = await bearer.GetAsync(HostFactory.JobsUrl, Cancellation);
        var unknown = await bearer.GetAsync(HostFactory.UnknownUrl, Cancellation);

        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        mismatch.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreach (var (response, status) in new[] { (refused, 401), (unknown, 404) })
        {
            var problem = await response.Content.ReadFromJsonAsync(
                ApiJsonContext.Default.ProblemBody,
                Cancellation
            );
            problem!.Status.Should().Be(status);
        }
    }

    [Fact]
    public async Task The_health_check_answers_without_the_token()
    {
        await using var factory = new HostFactory();
        using var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync(HostFactory.HealthUrl, Cancellation);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_login_cookie_opens_the_api_until_the_token_changes()
    {
        await using var factory = new HostFactory();
        using var client = factory.CreateCookieClient();

        var wrong = await LoginAsync(client, "wrong");
        var login = await LoginAsync(client, HostFactory.Token);
        var signedIn = await client.GetAsync(HostFactory.JobsUrl, Cancellation);
        var configuration = (IConfigurationRoot)
            factory.Services.GetRequiredService<IConfiguration>();
        configuration["Auth:Token"] = "another-token-0123456789";
        configuration.Reload();
        var signedOut = await client.GetAsync(HostFactory.JobsUrl, Cancellation);

        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        login.StatusCode.Should().Be(HttpStatusCode.NoContent);
        signedIn.StatusCode.Should().Be(HttpStatusCode.OK);
        signedOut.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_cookie_request_that_changes_state_needs_the_same_origin_and_a_bearer_request_does_not()
    {
        await using var factory = new HostFactory();
        using var cookie = factory.CreateCookieClient();
        using var bearer = factory.CreateBearerClient();
        await LoginAsync(cookie, HostFactory.Token);
        using var sameOrigin = new HttpRequestMessage(HttpMethod.Post, HostFactory.UnknownRetryUrl);
        sameOrigin.Headers.Add(OriginFilter.SecFetchSiteHeader, "same-origin");

        var crossSite = await cookie.PostAsync(
            HostFactory.UnknownRetryUrl,
            content: null,
            Cancellation
        );
        var allowed = await cookie.SendAsync(sameOrigin, Cancellation);
        var token = await bearer.PostAsync(
            HostFactory.UnknownRetryUrl,
            content: null,
            Cancellation
        );

        crossSite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        allowed.StatusCode.Should().Be(HttpStatusCode.NotFound);
        token.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string token) =>
        client.PostAsJsonAsync(
            HostFactory.LoginUrl,
            new LoginRequest(token),
            ApiJsonContext.Default.LoginRequest,
            Cancellation
        );
}
