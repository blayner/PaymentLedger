namespace PaymentLedger.Domain.Common;

/// <summary>
/// Event-sourced aggregate base. State is never mutated directly: behaviour methods
/// validate invariants and <see cref="Raise"/> an event, and <see cref="When"/> is the
/// only place that changes state. Replaying the same events always yields the same state.
/// </summary>
public abstract class AggregateRoot : IAggregateRoot
{
    private readonly List<IDomainEvent> _uncommittedEvents = [];

    public Guid Id { get; protected set; }

    public long Version { get; private set; } = -1;

    public IReadOnlyCollection<IDomainEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    public void LoadFromHistory(IEnumerable<IDomainEvent> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        foreach (var @event in history)
        {
            When(@event);
            Version++;
        }
    }

    public void MarkCommitted()
    {
        Version += _uncommittedEvents.Count;
        _uncommittedEvents.Clear();
    }

    protected void Raise(IDomainEvent @event)
    {
        When(@event);
        _uncommittedEvents.Add(@event);
    }

    protected abstract void When(IDomainEvent @event);
}
