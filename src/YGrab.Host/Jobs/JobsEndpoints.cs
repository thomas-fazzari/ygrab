// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net.Mime;
using Microsoft.AspNetCore.StaticFiles;
using YGrab.Contracts;
using YGrab.Contracts.Jobs;
using YGrab.Domain;
using YGrab.Engine;
using YGrab.Engine.Jobs;
using YGrab.Host.Extensions;

namespace YGrab.Host.Jobs;

internal static class JobsEndpoints
{
    private static readonly FileExtensionContentTypeProvider _contentTypes = new();

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapJobs()
        {
            var jobs = endpoints.MapGroup("/jobs");
            jobs.MapPost("/", StartAsync);
            jobs.MapGet("/", ListAsync);
            jobs.MapGet("/{id:guid}", GetAsync);
            jobs.MapGet("/{id:guid}/file", GetFileAsync);
            jobs.MapPost("/{id:guid}/retry", RetryAsync);
            jobs.MapPost("/{id:guid}/move", MoveAsync);
            jobs.MapDelete("/{id:guid}", RemoveAsync);
            endpoints.MapGet("/cooldown", GetCooldownAsync);
            endpoints.MapDelete("/cooldown", EndCooldownAsync);
            return endpoints;
        }
    }

    private static async Task<Results<Created<StartedJobDto>, ProblemHttpResult>> StartAsync(
        StartJobRequest request,
        IRequestHandler<StartJob.StartJobRequest, StartJob.Outcome> handler,
        CancellationToken cancellationToken
    )
    {
        var command = new StartJob.StartJobRequest(
            request.Link,
            request.Force,
            request.Mode.ToDomain(),
            request.Quality.ToDomain()
        );
        return await handler.HandleAsync(command, cancellationToken) switch
        {
            StartJob.Started started => TypedResults.Created(
                $"/api/jobs/{started.Id.Value}",
                new StartedJobDto(started.Id.Value)
            ),
            Failure failure => failure.ToProblem(),
            null => throw Unreachable.NullUnion(),
        };
    }

    private static async Task<Results<Ok<ImmutableArray<JobDto>>, ProblemHttpResult>> ListAsync(
        int? limit,
        IRequestHandler<ListJobs.ListJobsRequest, ListJobs.Outcome> handler,
        IRetentionSource retention,
        CancellationToken cancellationToken
    )
    {
        var outcome = await handler.HandleAsync(
            new ListJobs.ListJobsRequest(limit ?? ListJobs.MaxLimit),
            cancellationToken
        );
        return outcome.Value switch
        {
            ListJobs.Listed listed => TypedResults.Ok(
                ImmutableArray.CreateRange(listed.Jobs, job => job.ToDto(retention.Current))
            ),
            Failure failure => failure.ToProblem(),
            _ => throw Unreachable.NullUnion(),
        };
    }

    private static async Task<Results<Ok<JobDto>, ProblemHttpResult>> GetAsync(
        Guid id,
        IRequestHandler<GetJob.GetJobRequest, GetJob.Outcome> handler,
        IRetentionSource retention,
        CancellationToken cancellationToken
    ) =>
        await handler.HandleAsync(new GetJob.GetJobRequest(new JobId(id)), cancellationToken) switch
        {
            Job job => TypedResults.Ok(job.ToDto(retention.Current)),
            Failure failure => failure.ToProblem(),
            null => throw Unreachable.NullUnion(),
        };

    private static async Task<Results<PhysicalFileHttpResult, ProblemHttpResult>> GetFileAsync(
        Guid id,
        IRequestHandler<GetJobFile.GetJobFileRequest, GetJobFile.Outcome> handler,
        CancellationToken cancellationToken
    )
    {
        var outcome = await handler.HandleAsync(
            new GetJobFile.GetJobFileRequest(new JobId(id)),
            cancellationToken
        );
        return outcome.Value switch
        {
            JobFile file => TypedResults.PhysicalFile(
                file.Path,
                ContentTypeOf(file.Name),
                file.Name,
                enableRangeProcessing: true
            ),
            Failure failure => failure.ToProblem(),
            _ => throw Unreachable.NullUnion(),
        };
    }

    private static async Task<Results<Accepted, ProblemHttpResult>> RetryAsync(
        Guid id,
        IRequestHandler<RetryJob.RetryJobRequest, RetryJob.Outcome> handler,
        CancellationToken cancellationToken
    ) =>
        await handler.HandleAsync(
            new RetryJob.RetryJobRequest(new JobId(id)),
            cancellationToken
        ) switch
        {
            RetryJob.Retried => TypedResults.Accepted(new Uri($"/api/jobs/{id}", UriKind.Relative)),
            Failure failure => failure.ToProblem(),
            null => throw Unreachable.NullUnion(),
        };

    private static async Task<Results<NoContent, ProblemHttpResult>> MoveAsync(
        Guid id,
        MoveJobRequest request,
        IRequestHandler<MoveJob.MoveJobRequest, MoveJob.Outcome> handler,
        CancellationToken cancellationToken
    )
    {
        MoveJob.Destination to = request.Before is { } before
            ? new MoveJob.Destination.Before(new JobId(before))
            : new MoveJob.Destination.Last();
        return await handler.HandleAsync(
            new MoveJob.MoveJobRequest(new JobId(id), to),
            cancellationToken
        ) switch
        {
            MoveJob.Moved => TypedResults.NoContent(),
            Failure failure => failure.ToProblem(),
            null => throw Unreachable.NullUnion(),
        };
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveAsync(
        Guid id,
        IRequestHandler<RemoveJob.RemoveJobRequest, RemoveJob.Outcome> handler,
        CancellationToken cancellationToken
    ) =>
        await handler.HandleAsync(
            new RemoveJob.RemoveJobRequest(new JobId(id)),
            cancellationToken
        ) switch
        {
            RemoveJob.Removed => TypedResults.NoContent(),
            Failure failure => failure.ToProblem(),
            null => throw Unreachable.NullUnion(),
        };

    private static async Task<Ok<CooldownDto>> GetCooldownAsync(
        IRequestHandler<GetCooldown.GetCooldownRequest, Cooldown> handler,
        CancellationToken cancellationToken
    ) =>
        TypedResults.Ok(
            (
                await handler.HandleAsync(new GetCooldown.GetCooldownRequest(), cancellationToken)
            ).ToDto()
        );

    private static async Task<NoContent> EndCooldownAsync(
        IRequestHandler<EndCooldown.EndCooldownRequest, Done> handler,
        CancellationToken cancellationToken
    )
    {
        await handler.HandleAsync(new EndCooldown.EndCooldownRequest(), cancellationToken);
        return TypedResults.NoContent();
    }

    private static string ContentTypeOf(string fileName) =>
        _contentTypes.TryGetContentType(fileName, out var contentType)
            ? contentType
            : MediaTypeNames.Application.Octet;
}
