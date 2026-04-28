namespace PaymentLedger.Infrastructure.Outbox;

public sealed class InMemoryOutboxStore(TimeProvider clock) : IOutboxStore
{
    private readonly object _gate = new();
    private readonly List<OutboxMessage> _messages = [];
    private readonly SemaphoreSlim _processing = new(1, 1);

    /// <summary>Called by the in-memory event store while it holds its append lock.</summary>
    internal void Enqueue(IEnumerable<OutboxMessage> messages)
    {
        lock (_gate)
        {
            _messages.AddRange(messages);
        }
    }

    public async Task<OutboxBatchResult> ProcessPendingAsync(
        int batchSize,
        int maxAttempts,
        Func<OutboxMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        await _processing.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            OutboxMessage[] batch;
            lock (_gate)
            {
                batch = _messages
                    .Where(m => m.ProcessedAt is null && m.Attempts < maxAttempts)
                    .OrderBy(m => m.OccurredAt)
                    .Take(batchSize)
                    .ToArray();
            }

            var succeeded = 0;
            var failed = 0;

            foreach (var message in batch)
            {
                try
                {
                    await handler(message, cancellationToken).ConfigureAwait(false);
                    lock (_gate)
                    {
                        message.ProcessedAt = clock.GetUtcNow();
                        message.Attempts++;
                    }

                    succeeded++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lock (_gate)
                    {
                        message.Attempts++;
                        message.LastError = ex.Message;
                    }

                    failed++;
                }
            }

            return new OutboxBatchResult(succeeded, failed);
        }
        finally
        {
            _processing.Release();
        }
    }

    public Task<OutboxStats> GetStatsAsync(int maxAttempts, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var pending = _messages.Where(m => m.ProcessedAt is null && m.Attempts < maxAttempts).ToArray();
            var deadLetters = _messages.LongCount(m => m.ProcessedAt is null && m.Attempts >= maxAttempts);

            return Task.FromResult(new OutboxStats(
                pending.LongLength,
                pending.Length == 0 ? null : pending.Min(m => m.OccurredAt),
                deadLetters));
        }
    }
}
