// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;

namespace YGrab.Tests.Fakes;

/// <summary>
/// Answers each request with the body of its path, fails it, or never answers.
/// </summary>
internal sealed class FakeHttpClientFactory : IHttpClientFactory, IDisposable
{
    private readonly Uri _baseAddress;
    private readonly Handler _handler;

    public FakeHttpClientFactory(Uri baseAddress)
    {
        _baseAddress = baseAddress;
        _handler = new Handler(this);
    }

    public Dictionary<string, string> Bodies { get; } = [with(StringComparer.Ordinal)];

    public List<string> Paths { get; } = [];

    public Exception? Failure { get; set; }

    public bool Hangs { get; set; }

    public TaskCompletionSource Hanging { get; } = new();

    public HttpClient CreateClient(string name) =>
        new(_handler, disposeHandler: false) { BaseAddress = _baseAddress };

    public void Dispose() => _handler.Dispose();

    private sealed class Handler(FakeHttpClientFactory owner) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var path = request.RequestUri!.AbsolutePath;
            owner.Paths.Add(path);
            if (owner.Failure is { } failure)
            {
                throw failure;
            }

            if (owner.Hangs)
            {
                owner.Hanging.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return owner.Bodies.TryGetValue(path, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
