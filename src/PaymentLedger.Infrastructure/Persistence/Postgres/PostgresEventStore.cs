using System.Runtime.CompilerServices;
using Npgsql;
using NpgsqlTypes;
using PaymentLedger.Domain.Abstractions;
using PaymentLedger.Infrastructure.Serialization;

namespace PaymentLedger.Infrastructure.Persistence.Postgres;

/// <summary>
/// PostgreSQL-backed event store. Events and their outbox messages are written in one
/// transaction. Appends take a transaction-scoped advisory lock so <c>global_position</c>
/// is assigned and committed in the same order; without it, a reader tailing the log could
/// skip an event whose identity value was allocated earlier but committed later.
/// The price is that appends are serialised; see ADR-0004 for the trade-off.
/// </summary>
public sealed class PostgresEventStore(
    NpgsqlDataSource dataSource,
    EventSerializer serializer,
    TimeProvider clock) : IEventStore
{
    private const int ReadAllPageSize = 500;

    public async Task<IReadOnlyList<StoredEvent>> ReadStreamAsync(Guid streamId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT stream_id, version, global_position, event_type, data::text, recorded_at
            FROM events
            WHERE stream_id = @stream_id
            ORDER BY version
            """);
        command.Parameters.AddWithValue("stream_id", streamId);

        var events = new List<StoredEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(ReadEvent(reader));
        }

        return events;
    }

    public async Task AppendAsync(IReadOnlyCollection<StreamAppend> appends, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appends);
        if (appends.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key)", connection, transaction))
            {
                lockCommand.Parameters.AddWithValue("key", PostgresSchema.AppendLockKey);
                await lockCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            var recordedAt = clock.GetUtcNow();

            foreach (var append in appends)
            {
                var current = await CurrentVersionAsync(connection, transaction, append.StreamId, cancellationToken);
                if (current != append.ExpectedVersion)
                {
                    throw new ConcurrencyException(append.StreamId, append.ExpectedVersion, current);
                }

                var version = append.ExpectedVersion;
                foreach (var @event in append.Events)
                {
                    var eventType = serializer.GetEventType(@event);
                    var payload = serializer.Serialize(@event);

                    await using var insert = new NpgsqlCommand(
                        """
                        INSERT INTO events (event_id, stream_id, version, event_type, data, recorded_at)
                        VALUES (@event_id, @stream_id, @version, @event_type, @data, @recorded_at);

                        INSERT INTO outbox_messages (id, stream_id, message_type, payload, occurred_at)
                        VALUES (@event_id, @stream_id, @event_type, @data, @occurred_at);
                        """,
                        connection,
                        transaction);

                    insert.Parameters.AddWithValue("event_id", @event.EventId);
                    insert.Parameters.AddWithValue("stream_id", append.StreamId);
                    insert.Parameters.AddWithValue("version", ++version);
                    insert.Parameters.AddWithValue("event_type", eventType);
                    insert.Parameters.Add(new NpgsqlParameter("data", NpgsqlDbType.Jsonb) { Value = payload });
                    insert.Parameters.AddWithValue("recorded_at", recordedAt);
                    insert.Parameters.AddWithValue("occurred_at", @event.OccurredAt.ToUniversalTime());

                    await insert.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Safety net: the advisory lock should make this unreachable, but the unique
            // (stream_id, version) constraint is the real guarantee.
            var first = appends.First();
            throw new ConcurrencyException(first.StreamId, first.ExpectedVersion, actualVersion: -1);
        }
    }

    public async IAsyncEnumerable<StoredEvent> ReadAllAsync(
        long fromPosition = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var position = fromPosition;

        while (true)
        {
            var page = new List<StoredEvent>(ReadAllPageSize);

            await using (var command = dataSource.CreateCommand(
                             """
                             SELECT stream_id, version, global_position, event_type, data::text, recorded_at
                             FROM events
                             WHERE global_position > @position
                             ORDER BY global_position
                             LIMIT @limit
                             """))
            {
                command.Parameters.AddWithValue("position", position);
                command.Parameters.AddWithValue("limit", ReadAllPageSize);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    page.Add(ReadEvent(reader));
                }
            }

            foreach (var stored in page)
            {
                yield return stored;
            }

            if (page.Count < ReadAllPageSize)
            {
                yield break;
            }

            position = page[^1].GlobalPosition;
        }
    }

    private static async Task<long> CurrentVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid streamId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COALESCE(MAX(version), -1) FROM events WHERE stream_id = @stream_id",
            connection,
            transaction);
        command.Parameters.AddWithValue("stream_id", streamId);

        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private StoredEvent ReadEvent(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            serializer.Deserialize(reader.GetString(3), reader.GetString(4)),
            reader.GetFieldValue<DateTimeOffset>(5));
}
