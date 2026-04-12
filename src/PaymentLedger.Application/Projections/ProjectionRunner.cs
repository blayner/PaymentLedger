using Microsoft.Extensions.Logging;
using PaymentLedger.Application.Abstractions;
using PaymentLedger.Domain.Abstractions;

namespace PaymentLedger.Application.Projections;

/// <summary>
/// Tails the global event log from a checkpoint and applies events to every projection.
/// Reading the ordered log, rather than reacting to in-process notifications, guarantees
/// projections see events in commit order even when writes complete concurrently,
/// and lets a fresh process rebuild its read models from position zero.
/// </summary>
public sealed class ProjectionRunner(
    IEventStore eventStore,
    IEnumerable<IProjection> projections,
    ILogger<ProjectionRunner> logger) : IProjectionRunner
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly IProjection[] _projections = projections.ToArray();
    private long _checkpoint;

    public long Checkpoint => Interlocked.Read(ref _checkpoint);

    public async Task CatchUpAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var applied = 0;
            await foreach (var stored in eventStore.ReadAllAsync(_checkpoint, cancellationToken).ConfigureAwait(false))
            {
                foreach (var projection in _projections)
                {
                    await projection.ApplyAsync(stored, cancellationToken).ConfigureAwait(false);
                }

                Interlocked.Exchange(ref _checkpoint, stored.GlobalPosition);
                applied++;
            }

            if (applied > 0)
            {
                logger.LogDebug("Projected {Count} events; checkpoint is now {Checkpoint}", applied, _checkpoint);
            }
        }
        finally
        {
            _mutex.Release();
        }
    }
}
