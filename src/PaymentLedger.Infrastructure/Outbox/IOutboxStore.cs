namespace PaymentLedger.Infrastructure.Outbox;

public interface IOutboxStore
{
    /// <summary>
    /// Claims up to <paramref name="batchSize"/> pending messages, runs <paramref name="handler"/>
    /// for each one, and records success or failure. Messages that reached
    /// <paramref name="maxAttempts"/> are left alone (dead-lettered) for manual inspection.
    /// The store owns claiming, so several processors can run without double-publishing a batch.
    /// </summary>
    Task<OutboxBatchResult> ProcessPendingAsync(
        int batchSize,
        int maxAttempts,
        Func<OutboxMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken);

    Task<OutboxStats> GetStatsAsync(int maxAttempts, CancellationToken cancellationToken);
}

public readonly record struct OutboxBatchResult(int Succeeded, int Failed)
{
    public int Total => Succeeded + Failed;
}

public sealed record OutboxStats(long PendingCount, DateTimeOffset? OldestPendingAt, long DeadLetterCount);
