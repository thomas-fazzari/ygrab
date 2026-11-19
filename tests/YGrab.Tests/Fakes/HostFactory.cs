// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using YGrab.Engine.Jobs;
using YGrab.Host.Auth;

namespace YGrab.Tests.Fakes;

/// <summary>
/// The host with an in-memory job store and none of its background services.
/// </summary>
internal sealed class HostFactory : WebApplicationFactory<Program>
{
    public const string Token = "test-token-0123456789";

    public static readonly Uri JobsUrl = Relative("/api/jobs");
    public static readonly Uri EventsUrl = Relative("/api/events");
    public static readonly Uri LoginUrl = Relative("/api/login");
    public static readonly Uri HealthUrl = Relative("/api/health");
    public static readonly Uri UnknownUrl = Relative("/api/nothing");
    public static readonly Uri UnknownRetryUrl = Relative(
        "/api/jobs/00000000-0000-0000-0000-000000000001/retry"
    );

    private readonly TempDirectory _data = new();

    public HttpClient CreateBearerClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            BearerHandler.SchemeName,
            Token
        );
        return client;
    }

    public HttpClient CreateCookieClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder
            .UseSetting("Storage:DataDirectory", _data.FullName)
            .UseSetting("Auth:Token", Token)
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IJobStore>();
                services.AddSingleton<IJobStore, InMemoryJobStore>();
            });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _data.Dispose();
    }

    private static Uri Relative(string path) => new(path, UriKind.Relative);
}
