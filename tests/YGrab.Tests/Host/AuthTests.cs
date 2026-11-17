// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
    public async Task A_request_that_changes_state_needs_the_same_origin_or_a_bearer_token()
    {
        await using var factory = new HostFactory();
        using var cookie = factory.CreateCookieClient();
        using var bearer = factory.CreateBearerClient();
        using var anonymous = factory.CreateClient();
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
        var crossSiteLogin = await anonymous.PostAsJsonAsync(
            HostFactory.LoginUrl,
            new LoginRequest(HostFactory.Token),
            ApiJsonContext.Default.LoginRequest,
            Cancellation
        );

        crossSite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        allowed.StatusCode.Should().Be(HttpStatusCode.NotFound);
        token.StatusCode.Should().Be(HttpStatusCode.NotFound);
        crossSiteLogin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_host_refuses_to_start_without_auth_outside_development()
    {
        await using var factory = new HostFactory();
        await using var production = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment(Environments.Production).UseSetting("Auth:Disabled", "true")
        );

        var start = () => production.CreateClient();

        start.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public async Task Without_auth_a_request_that_changes_state_still_needs_the_same_origin_or_a_bearer_token()
    {
        await using var factory = new HostFactory();
        await using var open = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:Disabled", "true")
        );
        using var anonymous = open.CreateClient();
        using var bearer = open.CreateClient();
        bearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            BearerHandler.SchemeName,
            "any"
        );

        var crossSite = await anonymous.PostAsync(
            HostFactory.UnknownRetryUrl,
            content: null,
            Cancellation
        );
        var token = await bearer.PostAsync(
            HostFactory.UnknownRetryUrl,
            content: null,
            Cancellation
        );

        crossSite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        token.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Too_many_wrong_tokens_refuse_the_right_one_at_login()
    {
        await using var factory = new HostFactory();
        using var client = factory.CreateCookieClient();

        for (var attempt = 0; attempt < WrongTokenLimiter.Tries; attempt++)
        {
            var wrong = await LoginAsync(client, "wrong-token-0123456789");
            wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var right = await LoginAsync(client, HostFactory.Token);

        right.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Wrong_bearer_tokens_count_by_the_forwarded_address_only_from_a_private_network()
    {
        await using var factory = new HostFactory();
        const string publicClient = "203.0.113.5";
        // A container sees the proxy through the Docker bridge, as an IPv4 address mapped to IPv6
        const string proxy = "::ffff:172.18.0.1";

        for (var attempt = 0; attempt < WrongTokenLimiter.Tries; attempt++)
        {
            var forwardedFor = $"198.51.100.{attempt}";
            (await SendBearerAsync(factory, publicClient, forwardedFor, "wrong"))
                .Should()
                .Be(StatusCodes.Status401Unauthorized);
            (await SendBearerAsync(factory, proxy, forwardedFor, "wrong"))
                .Should()
                .Be(StatusCodes.Status401Unauthorized);
        }

        var spoofed = await SendBearerAsync(
            factory,
            publicClient,
            "198.51.100.99",
            HostFactory.Token
        );
        var proxied = await SendBearerAsync(factory, proxy, "198.51.100.99", HostFactory.Token);

        spoofed.Should().Be(StatusCodes.Status401Unauthorized);
        proxied.Should().Be(StatusCodes.Status200OK);
    }

    private static async Task<int> SendBearerAsync(
        HostFactory factory,
        string remoteAddress,
        string forwardedFor,
        string token
    )
    {
        var context = await factory.Server.SendAsync(
            http =>
            {
                http.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
                http.Request.Method = HttpMethods.Get;
                http.Request.Path = HostFactory.JobsUrl.OriginalString;
                http.Request.Headers["X-Forwarded-For"] = forwardedFor;
                http.Request.Headers.Authorization = $"{BearerHandler.SchemeName} {token}";
            },
            Cancellation
        );
        return context.Response.StatusCode;
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, HostFactory.LoginUrl)
        {
            Content = JsonContent.Create(
                new LoginRequest(token),
                ApiJsonContext.Default.LoginRequest
            ),
        };
        request.Headers.Add(OriginFilter.SecFetchSiteHeader, "same-origin");
        return await client.SendAsync(request, Cancellation);
    }
}
