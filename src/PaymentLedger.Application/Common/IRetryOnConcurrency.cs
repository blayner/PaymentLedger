namespace PaymentLedger.Application.Common;

/// <summary>
/// Marks a command as safe to re-run from scratch when an optimistic concurrency
/// conflict occurs. Handlers of such commands must reload state on every attempt
/// and be idempotent on their transaction id.
/// </summary>
public interface IRetryOnConcurrency
{
}
