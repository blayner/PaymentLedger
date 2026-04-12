using PaymentLedger.Domain.Abstractions;

namespace PaymentLedger.Application.Abstractions;

/// <summary>Builds a read model from the global event log. Events arrive strictly in commit order.</summary>
public interface IProjection
{
    Task ApplyAsync(StoredEvent storedEvent, CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);
}

public interface IProjectionRunner
{
    /// <summary>Feeds every event committed since the last checkpoint to all projections.</summary>
    Task CatchUpAsync(CancellationToken cancellationToken);

    long Checkpoint { get; }
}
