namespace PaymentLedger.Domain.Common;

/// <summary>
/// Base record for domain events. Events are immutable facts; once persisted they are
/// never updated, only superseded by newer events.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
