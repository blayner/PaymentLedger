namespace PaymentLedger.Domain.Accounts;

/// <summary>Outcome of checking an idempotency key against an account's applied transactions.</summary>
public enum IdempotencyCheck
{
    /// <summary>The transaction id has not moved this account yet.</summary>
    NotApplied,

    /// <summary>Same key, same request: return the original result without posting again.</summary>
    Replay,

    /// <summary>Same key, different request: the client reused a key and must be told so.</summary>
    PayloadMismatch,
}
