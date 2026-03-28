using System.Runtime.CompilerServices;
using PaymentLedger.Domain.Abstractions;

namespace PaymentLedger.Infrastructure.EventStore;

/// <summary>
/// Process-local event store used for development and tests. A single lock makes
/// multi-stream appends atomic, which mirrors the guarantee of the database-backed store.
/// </summary>
public sealed class InMemoryEventStore(TimeProvider clock) : IEventStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, List<StoredEvent>> _streams = [];
    private readonly List<StoredEvent> _log = [];

    public Task<IReadOnlyList<StoredEvent>> ReadStreamAsync(Guid streamId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<StoredEvent> result = _streams.TryGetValue(streamId, out var stream)
                ? stream.ToArray()
                : Array.Empty<StoredEvent>();
            return Task.FromResult(result);
        }
    }

    public Task AppendAsync(IReadOnlyCollection<StreamAppend> appends, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appends);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            // Validate every stream first so a conflict on one leaves all of them untouched.
            foreach (var append in appends)
            {
                var current = CurrentVersion(append.StreamId);
                if (current != append.ExpectedVersion)
                {
                    throw new ConcurrencyException(append.StreamId, append.ExpectedVersion, current);
                }
            }

            var recordedAt = clock.GetUtcNow();

            foreach (var append in appends)
            {
                if (!_streams.TryGetValue(append.StreamId, out var stream))
                {
                    stream = [];
                    _streams[append.StreamId] = stream;
                }

                var version = append.ExpectedVersion;
                foreach (var @event in append.Events)
                {
                    var stored = new StoredEvent(append.StreamId, ++version, _log.Count + 1, @event, recordedAt);
                    stream.Add(stored);
                    _log.Add(stored);
                }
            }
        }

        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<StoredEvent> ReadAllAsync(
        long fromPosition = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StoredEvent[] snapshot;
        lock (_gate)
        {
            snapshot = _log.Where(e => e.GlobalPosition > fromPosition).ToArray();
        }

        foreach (var stored in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return stored;
        }

        await Task.CompletedTask;
    }

    private long CurrentVersion(Guid streamId) =>
        _streams.TryGetValue(streamId, out var stream) ? stream.Count - 1 : -1;
}
