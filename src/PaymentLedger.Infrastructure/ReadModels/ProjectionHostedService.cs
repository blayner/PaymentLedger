using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentLedger.Application.Abstractions;

namespace PaymentLedger.Infrastructure.ReadModels;

public sealed class ProjectionOptions
{
    public const string SectionName = "Projections";

    /// <summary>
    /// How often to poll the event log for events written by other instances.
    /// Writes made through this instance are projected immediately after commit.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);
}

/// <summary>Rebuilds read models from the event log on startup, then keeps polling for new events.</summary>
public sealed class ProjectionHostedService(
    IProjectionRunner runner,
    IOptions<ProjectionOptions> options,
    ILogger<ProjectionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Rebuilding read models from the event log");
        await runner.CatchUpAsync(stoppingToken);
        logger.LogInformation("Read models rebuilt up to position {Checkpoint}", runner.Checkpoint);

        using var timer = new PeriodicTimer(options.Value.PollingInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await runner.CatchUpAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Projection catch-up failed; will retry on next tick");
            }
        }
    }
}
