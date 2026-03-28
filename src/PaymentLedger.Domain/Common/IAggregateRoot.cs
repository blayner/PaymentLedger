namespace PaymentLedger.Domain.Common;

public interface IAggregateRoot
{
    Guid Id { get; }

    /// <summary>Version of the last persisted event, or -1 for an aggregate that was never saved.</summary>
    long Version { get; }

    IReadOnlyCollection<IDomainEvent> UncommittedEvents { get; }

    void MarkCommitted();
}
