using DotNet.Testcontainers.Builders;
using Npgsql;
using Testcontainers.PostgreSql;

namespace PaymentLedger.Integration.Tests.Infrastructure;

/// <summary>
/// One PostgreSQL container for the whole test run. Each test class asks for its own
/// database, so tests are isolated without paying a container start-up per class.
/// </summary>
/// <remarks>
/// On a developer machine without Docker the tests are reported as skipped instead of failed.
/// In CI (the <c>CI</c> environment variable is set) a missing Docker daemon stays a hard failure.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    /// <summary>Set when Docker is unavailable outside CI; test classes skip themselves.</summary>
    public string? SkipReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            // Build() already resolves the Docker endpoint, so it belongs inside the guard.
            _container = new PostgreSqlBuilder("postgres:18").Build();
            await _container.StartAsync();
        }
        catch (DockerUnavailableException ex) when (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
        {
            SkipReason = $"Docker is not available, so PostgreSQL integration tests are skipped: {ex.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public async Task<string> CreateDatabaseAsync()
    {
        Assert.SkipWhen(SkipReason is not null, SkipReason ?? string.Empty);

        var name = $"ledger_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(_container!.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_container!.GetConnectionString()) { Database = name }.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
