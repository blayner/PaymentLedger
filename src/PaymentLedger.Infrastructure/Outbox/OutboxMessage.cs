namespace PaymentLedger.Infrastructure.Outbox;

/// <summary>
/// A message waiting to be published to the outside world. It is written in the same
/// transaction as the events it describes, so a crash can delay publication but never
/// lose it. Delivery is at-least-once; <see cref="Id"/> equals the domain event id and
/// is the deduplication key for consumers.
/// </summary>
public sealed class OutboxMessage
{
    public required Guid Id { get; init; }

    public required Guid StreamId { get; init; }

    public required string MessageType { get; init; }

    public required string Payload { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
