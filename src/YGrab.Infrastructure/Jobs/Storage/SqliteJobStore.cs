// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;
using YGrab.Engine.Jobs;
using YGrab.Infrastructure.Database;

namespace YGrab.Infrastructure.Jobs;

/// <summary>
/// Stores each job and the cooldown as one row each.
/// </summary>
/// <remarks>
/// A save writes the whole row, so a repeated save is safe. It updates no row for a deleted job.
/// </remarks>
internal sealed class SqliteJobStore(SqliteDatabase database, SqliteWriter writer) : IJobStore
{
    private const string SelectJobsSql = $"""
        SELECT {JobRow.SelectColumns}
        FROM jobs
        """;

    private const string SelectCooldownSql = """
        SELECT strikes AS Strikes, until AS Until, reason AS Reason
        FROM cooldown
        WHERE id = 1
        """;

    private const string InsertJobSql = """
        INSERT INTO jobs (
            id, url, link_kind, link_id, quality, force, status, created_at, release, tracks,
            video_title, video_channel, attempt, retry_at, error, file_name, file_size,
            duplicate_of, finished_at, enrichment_failure)
        VALUES (
            @Id, @Url, @LinkKind, @LinkId, @Quality, @Force, @Status, @CreatedAt, @Release, @Tracks,
            @VideoTitle, @VideoChannel, @Attempt, @RetryAt, @Error, @FileName, @FileSize,
            @DuplicateOf, @FinishedAt, @EnrichmentFailure)
        """;

    private const string UpdateJobSql = """
        UPDATE jobs SET
            url = @Url, link_kind = @LinkKind, link_id = @LinkId, quality = @Quality,
            force = @Force, status = @Status, created_at = @CreatedAt, release = @Release,
            tracks = @Tracks, video_title = @VideoTitle, video_channel = @VideoChannel,
            attempt = @Attempt, retry_at = @RetryAt, error = @Error, file_name = @FileName,
            file_size = @FileSize, duplicate_of = @DuplicateOf, finished_at = @FinishedAt,
            enrichment_failure = @EnrichmentFailure
        WHERE id = @Id
        """;

    private const string DeleteJobSql = """
        DELETE FROM jobs
        WHERE id = @Id
        """;

    private const string UpsertCooldownSql = """
        INSERT INTO cooldown (id, strikes, until, reason)
        VALUES (1, @Strikes, @Until, @Reason)
        ON CONFLICT (id) DO UPDATE SET
            strikes = excluded.strikes, until = excluded.until, reason = excluded.reason
        """;

    public async ValueTask<JobSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        await using var connection = database.OpenReadConnection();
        var rows = await connection.QueryAsync<JobRow>(
            new CommandDefinition(SelectJobsSql, cancellationToken: cancellationToken)
        );
        var cooldown = await connection.QuerySingleOrDefaultAsync<CooldownRow>(
            new CommandDefinition(SelectCooldownSql, cancellationToken: cancellationToken)
        );
        return new JobSnapshot([.. rows.Select(JobRowMapper.ToJob)], ToCooldown(cooldown));
    }

    public async ValueTask InsertAsync(Job job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        var row = JobRowMapper.ToRow(job);
        await writer.WriteAsync(
            connection => connection.Execute(InsertJobSql, row),
            cancellationToken
        );
    }

    public async ValueTask SaveAsync(Func<Job?> current, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(current);

        await writer.WriteAsync(
            connection =>
            {
                if (current() is { } job)
                {
                    connection.Execute(UpdateJobSql, JobRowMapper.ToRow(job));
                }
            },
            cancellationToken
        );
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var blob = GuidBlob.ToBlob(id);
        await writer.WriteAsync(
            connection => connection.Execute(DeleteJobSql, new { Id = blob }),
            cancellationToken
        );
    }

    public async ValueTask SaveAsync(Cooldown cooldown, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cooldown);

        var row = new CooldownRow(
            cooldown.Strikes,
            cooldown.Until?.ToUnixTimeMilliseconds(),
            cooldown.Reason
        );
        await writer.WriteAsync(
            connection => connection.Execute(UpsertCooldownSql, row),
            cancellationToken
        );
    }

    private static Cooldown ToCooldown(CooldownRow? row) =>
        row is null
            ? Cooldown.None
            : Cooldown.Restore(
                checked((int)row.Strikes),
                row.Until is { } until ? DateTimeOffset.FromUnixTimeMilliseconds(until) : null,
                row.Reason
            );

    private sealed record CooldownRow(long Strikes, long? Until, string? Reason);
}
