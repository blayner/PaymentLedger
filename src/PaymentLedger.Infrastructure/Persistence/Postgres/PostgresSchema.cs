namespace PaymentLedger.Infrastructure.Persistence.Postgres;

internal static class PostgresSchema
{
    /// <summary>
    /// Lock key used to serialise appends (and schema creation) across all instances.
    /// The DDL below repeats it as a literal; Npgsql runs a multi-statement command in one
    /// implicit transaction, so the transaction-scoped lock covers the whole script.
    /// </summary>
    public const long AppendLockKey = 0x5041594C45444752; // "PAYLEDGR"

    public const string Sql = """
        SELECT pg_advisory_xact_lock(5783001580634916690);

        CREATE TABLE IF NOT EXISTS events (
            global_position BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            event_id        UUID        NOT NULL UNIQUE,
            stream_id       UUID        NOT NULL,
            version         BIGINT      NOT NULL,
            event_type      TEXT        NOT NULL,
            data            JSONB       NOT NULL,
            recorded_at     TIMESTAMPTZ NOT NULL,
            CONSTRAINT uq_events_stream_version UNIQUE (stream_id, version)
        );

        CREATE TABLE IF NOT EXISTS outbox_messages (
            id           UUID        PRIMARY KEY,
            stream_id    UUID        NOT NULL,
            message_type TEXT        NOT NULL,
            payload      JSONB       NOT NULL,
            occurred_at  TIMESTAMPTZ NOT NULL,
            processed_at TIMESTAMPTZ NULL,
            attempts     INT         NOT NULL DEFAULT 0,
            last_error   TEXT        NULL
        );

        CREATE INDEX IF NOT EXISTS ix_outbox_pending
            ON outbox_messages (occurred_at)
            WHERE processed_at IS NULL;
        """;
}
