// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Host;
using YGrab.Host.Auth;
using YGrab.Host.Diagnostics;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Configuration.AddYGrabConfiguration();
builder.Services.AddYGrabEngine();
builder.Services.AddYGrabInfrastructure();
builder.Services.AddYGrabPython(builder.Configuration);
builder.Services.AddYGrabAuth();
builder.Services.AddYGrabEndpoints();
builder.Services.AddYGrabDiagnostics();
builder.Services.AddYGrabTelemetry(builder.Configuration);

var app = builder.Build();

app.UseYGrabHttp();
app.UseYGrabAuth();
app.MapYGrabEndpoints();

await app.RunAsync();
