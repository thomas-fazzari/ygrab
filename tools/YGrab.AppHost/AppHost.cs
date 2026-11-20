// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

var builder = DistributedApplication.CreateBuilder(args);

var host = builder
    .AddDotnetProject("host", "../../src/YGrab.Host/YGrab.Host.csproj")
    .WithHttpHealthCheck("/api/health");

builder
    .AddViteApp("web", "../../web")
    .WithBun(installArgs: ["--frozen-lockfile"])
    .WithEnvironment("YGRAB_HOST_URL", host.GetEndpoint("http"))
    .WaitFor(host);

await builder.Build().RunAsync(CancellationToken.None);
