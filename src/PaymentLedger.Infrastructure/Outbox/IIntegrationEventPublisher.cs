using Microsoft.Extensions.Logging;

namespace PaymentLedger.Infrastructure.Outbox;

/// <summary>Transport-specific publisher (RabbitMQ, Kafka, SNS, ...). Must throw on failure so the message is retried.</summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Default publisher that writes messages to the log. It keeps the pipeline observable
/// locally without a broker; swap it for a real transport adapter in production.
/// </summary>
public sealed class LoggingIntegrationEventPublisher(ILogger<LoggingIntegrationEventPublisher> logger)
    : IIntegrationEventPublisher
{
    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Published {MessageType} {MessageId} for stream {StreamId}",
            message.MessageType,
            message.Id,
            message.StreamId);

        return Task.CompletedTask;
    }
}
