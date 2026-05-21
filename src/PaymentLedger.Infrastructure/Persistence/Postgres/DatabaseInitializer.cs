using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace PaymentLedger.Infrastructure.Persistence.Postgres;

/// <summary>
/// Creates the event store schema on startup. Registered before every other hosted service,
/// so projections and the outbox processor never start against a missing table.
/// The DDL is idempotent and guarded by an advisory lock for concurrent instance start-ups.
/// </summary>
public sealed class DatabaseInitializer(NpgsqlDataSource dataSource, ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(PostgresSchema.Sql);
        await command.ExecuteNonQueryAsync(cancellationToken);
        logger.LogInformation("Event store schema is up to date");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
