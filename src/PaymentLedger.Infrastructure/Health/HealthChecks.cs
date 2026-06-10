using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;
using PaymentLedger.Infrastructure.Outbox;

namespace PaymentLedger.Infrastructure.Health;

public static class HealthTags
{
    /// <summary>The process is up. Failing this restarts the container.</summary>
    public const string Live = "live";

    /// <summary>Dependencies are reachable. Failing this takes the instance out of rotation.</summary>
    public const string Ready = "ready";
}

public sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unreachable.", ex);
        }
    }
}

/// <summary>
/// Reports Degraded (not Unhealthy) when publication lags or messages are dead-lettered:
/// the ledger still accepts writes correctly, but downstream consumers are falling behind.
/// </summary>
public sealed class OutboxHealthCheck(
    IOutboxStore outbox,
    IOptions<OutboxOptions> options,
    TimeProvider clock) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var stats = await outbox.GetStatsAsync(settings.MaxAttempts, cancellationToken);
        var lag = stats.OldestPendingAt is { } oldest ? clock.GetUtcNow() - oldest : TimeSpan.Zero;

        var data = new Dictionary<string, object>
        {
            ["pending"] = stats.PendingCount,
            ["deadLettered"] = stats.DeadLetterCount,
            ["lagSeconds"] = Math.Round(lag.TotalSeconds, 1),
        };

        if (stats.DeadLetterCount > 0)
        {
            return HealthCheckResult.Degraded($"{stats.DeadLetterCount} outbox message(s) exceeded the retry limit.", data: data);
        }

        if (lag > settings.MaxHealthyLag)
        {
            return HealthCheckResult.Degraded($"Outbox is lagging by {lag.TotalSeconds:0} s.", data: data);
        }

        return HealthCheckResult.Healthy(data: data);
    }
}
