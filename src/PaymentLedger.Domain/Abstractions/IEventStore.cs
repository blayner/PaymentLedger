using PaymentLedger.Domain.Common;

namespace PaymentLedger.Domain.Abstractions;

/// <summary>
/// Append-only store of domain events, organised in streams (one per aggregate).
/// </summary>
public interface IEventStore
{
    Task<IReadOnlyList<StoredEvent>> ReadStreamAsync(Guid streamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends events to one or more streams atomically: either every stream is written
    /// or none is. Each append carries the version the caller last saw; a mismatch
    /// raises <see cref="ConcurrencyException"/> (optimistic concurrency).
    /// </summary>
    Task AppendAsync(IReadOnlyCollection<StreamAppend> appends, CancellationToken cancellationToken = default);

    /// <summary>Reads every event across all streams in commit order, after <paramref name="fromPosition"/>.</summary>
    IAsyncEnumerable<StoredEvent> ReadAllAsync(long fromPosition = 0, CancellationToken cancellationToken = default);
}

public sealed record StreamAppend(Guid StreamId, long ExpectedVersion, IReadOnlyList<IDomainEvent> Events);

public sealed record StoredEvent(
    Guid StreamId,
    long Version,
    long GlobalPosition,
    IDomainEvent Event,
    DateTimeOffset RecordedAt);
