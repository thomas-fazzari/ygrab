// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using YGrab.Contracts;
using YGrab.Contracts.Jobs;

namespace YGrab.Cli.Api;

internal sealed class ApiClient(HttpClient http, Uri baseUrl, string token)
{
    public async Task<ApiResult<ImmutableArray<JobDto>>> ListJobsAsync(
        int? limit,
        CancellationToken cancellationToken
    )
    {
        var query = limit is { } value
            ? string.Create(CultureInfo.InvariantCulture, $"?limit={value}")
            : string.Empty;
        var sent = await SendAsync(
            HttpMethod.Get,
            $"api/jobs{query}",
            content: null,
            accept: null,
            cancellationToken
        );
        return await ReadJsonAsync(
            sent,
            ApiJsonContext.Default.ImmutableArrayJobDto,
            cancellationToken
        );
    }

    public async Task<ApiResult<StartedJobDto>> StartJobAsync(
        StartJobRequest request,
        CancellationToken cancellationToken
    )
    {
        using var content = JsonContent.Create(request, ApiJsonContext.Default.StartJobRequest);
        var sent = await SendAsync(
            HttpMethod.Post,
            "api/jobs",
            content,
            accept: null,
            cancellationToken
        );
        return await ReadJsonAsync(sent, ApiJsonContext.Default.StartedJobDto, cancellationToken);
    }

    public Task<ApiResult<None>> RetryJobAsync(Guid id, CancellationToken cancellationToken) =>
        SendEmptyAsync(HttpMethod.Post, JobPath(id, "/retry"), cancellationToken);

    public Task<ApiResult<None>> RemoveJobAsync(Guid id, CancellationToken cancellationToken) =>
        SendEmptyAsync(HttpMethod.Delete, JobPath(id, string.Empty), cancellationToken);

    /// <returns>The answer, whose body the caller reads and then disposes with it.</returns>
    public Task<ApiResult<HttpResponseMessage>> OpenFileAsync(
        Guid id,
        CancellationToken cancellationToken
    ) =>
        SendAsync(
            HttpMethod.Get,
            JobPath(id, "/file"),
            content: null,
            MediaTypeNames.Application.Octet,
            cancellationToken
        );

    /// <returns>The answer, whose body the caller reads and then disposes with it.</returns>
    public Task<ApiResult<HttpResponseMessage>> OpenEventsAsync(
        CancellationToken cancellationToken
    ) =>
        SendAsync(
            HttpMethod.Get,
            "api/events",
            content: null,
            MediaTypeNames.Text.EventStream,
            cancellationToken
        );

    private static string JobPath(Guid id, string suffix) =>
        string.Create(CultureInfo.InvariantCulture, $"api/jobs/{id:D}{suffix}");

    private async Task<ApiResult<None>> SendEmptyAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken
    )
    {
        var sent = await SendAsync(method, path, content: null, accept: null, cancellationToken);
        if (!sent.TryGetValue(out var response, out var failure))
        {
            return ApiResult<None>.Fail(failure);
        }

        response.Dispose();
        return ApiResult<None>.Ok(default);
    }

    private static async Task<ApiResult<T>> ReadJsonAsync<T>(
        ApiResult<HttpResponseMessage> sent,
        JsonTypeInfo<T> info,
        CancellationToken cancellationToken
    )
    {
        if (!sent.TryGetValue(out var response, out var failure))
        {
            return ApiResult<T>.Fail(failure);
        }

        using (response)
        {
            var value = await response.Content.ReadFromJsonAsync(info, cancellationToken);
            return value is null
                ? ApiResult<T>.Fail(
                    new ApiFailure.Rejected((int)response.StatusCode, Problem: null)
                )
                : ApiResult<T>.Ok(value);
        }
    }

    private async Task<ApiResult<HttpResponseMessage>> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        string? accept,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(method, new Uri(baseUrl, path))
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // JSON stays acceptable, so that an error of a file or stream request has its body
        if (accept is not null)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        }

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json)
        );
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );
        }
        catch (HttpRequestException exception)
        {
            return ApiResult<HttpResponseMessage>.Fail(
                new ApiFailure.Unreachable(Describe(exception))
            );
        }

        if (response.IsSuccessStatusCode)
        {
            return ApiResult<HttpResponseMessage>.Ok(response);
        }

        using (response)
        {
            return ApiResult<HttpResponseMessage>.Fail(
                await ToFailureAsync(response, cancellationToken)
            );
        }
    }

    private static async Task<ApiFailure> ToFailureAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new ApiFailure.BadToken();
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync(
                ApiJsonContext.Default.ProblemBody,
                cancellationToken
            );
            return new ApiFailure.Rejected((int)response.StatusCode, problem);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return new ApiFailure.Rejected((int)response.StatusCode, Problem: null);
        }
    }

    // A socket failure repeats the message of its inner exception in its own
    private static string Describe(HttpRequestException exception)
    {
        if (exception.InnerException is not { } inner)
        {
            return exception.Message;
        }

        return exception.Message.Contains(inner.Message, StringComparison.Ordinal)
            ? exception.Message
            : $"{exception.Message} {inner.Message}";
    }
}
