using Npgsql;
using NpgsqlTypes;
using PaymentLedger.Infrastructure.Outbox;

namespace PaymentLedger.Infrastructure.Persistence.Postgres;

/// <summary>
/// Claims outbox rows with <c>FOR UPDATE SKIP LOCKED</c>, so several processors (one per
/// API instance) can drain the outbox in parallel without publishing the same row twice
/// in the happy path. The claim is held while publishing; if the process dies before the
/// commit, the rows are released and published again (at-least-once delivery).
/// </summary>
public sealed class PostgresOutboxStore(NpgsqlDataSource dataSource, TimeProvider clock) : IOutboxStore
{
    public async Task<OutboxBatchResult> ProcessPendingAsync(
        int batchSize,
        int maxAttempts,
        Func<OutboxMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var batch = new List<OutboxMessage>(batchSize);
        await using (var select = new NpgsqlCommand(
                         """
                         SELECT id, stream_id, message_type, payload::text, occurred_at, attempts
                         FROM outbox_messages
                         WHERE processed_at IS NULL AND attempts < @max_attempts
                         ORDER BY occurred_at
                         LIMIT @batch_size
                         FOR UPDATE SKIP LOCKED
                         """,
                         connection,
                         transaction))
        {
            select.Parameters.AddWithValue("max_attempts", maxAttempts);
            select.Parameters.AddWithValue("batch_size", batchSize);

            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                batch.Add(new OutboxMessage
                {
                    Id = reader.GetGuid(0),
                    StreamId = reader.GetGuid(1),
                    MessageType = reader.GetString(2),
                    Payload = reader.GetString(3),
                    OccurredAt = reader.GetFieldValue<DateTimeOffset>(4),
                    Attempts = reader.GetInt32(5),
                });
            }
        }

        var succeeded = 0;
        var failed = 0;

        foreach (var message in batch)
        {
            string? error = null;
            try
            {
                await handler(message, cancellationToken);
                succeeded++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                error = ex.Message;
                failed++;
            }

            await using var update = new NpgsqlCommand(
                """
                UPDATE outbox_messages
                SET attempts = attempts + 1,
                    processed_at = CASE WHEN @error IS NULL THEN @now ELSE NULL END,
                    last_error = @error
                WHERE id = @id
                """,
                connection,
                transaction);
            update.Parameters.AddWithValue("id", message.Id);
            update.Parameters.AddWithValue("now", clock.GetUtcNow());
            update.Parameters.Add(new NpgsqlParameter("error", NpgsqlDbType.Text) { Value = (object?)error ?? DBNull.Value });
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new OutboxBatchResult(succeeded, failed);
    }

    public async Task<OutboxStats> GetStatsAsync(int maxAttempts, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT
                COUNT(*) FILTER (WHERE attempts < @max_attempts),
                MIN(occurred_at) FILTER (WHERE attempts < @max_attempts),
                COUNT(*) FILTER (WHERE attempts >= @max_attempts)
            FROM outbox_messages
            WHERE processed_at IS NULL
            """);
        command.Parameters.AddWithValue("max_attempts", maxAttempts);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new OutboxStats(
            reader.GetInt64(0),
            reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
            reader.GetInt64(2));
    }
}
