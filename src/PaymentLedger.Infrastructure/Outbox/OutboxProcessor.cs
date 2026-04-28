using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PaymentLedger.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = 100;

    public int MaxAttempts { get; set; } = 10;
}

/// <summary>
/// Polls the outbox and hands pending messages to the publisher. A full batch is
/// followed immediately by another one so a backlog drains without waiting for the timer.
/// </summary>
public sealed class OutboxProcessor(
    IOutboxStore store,
    IIntegrationEventPublisher publisher,
    IOptions<OutboxOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.PollingInterval);

        do
        {
            try
            {
                OutboxBatchResult result;
                do
                {
                    result = await store.ProcessPendingAsync(
                        _options.BatchSize,
                        _options.MaxAttempts,
                        publisher.PublishAsync,
                        stoppingToken);

                    if (result.Failed > 0)
                    {
                        logger.LogWarning("Outbox batch had {Failed} failed message(s)", result.Failed);
                    }
                }
                while (result.Succeeded == _options.BatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox processing failed; will retry on next tick");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
