using PaymentLedger.Domain.Abstractions;
using PaymentLedger.Domain.Accounts;

namespace PaymentLedger.Application.Common;

/// <summary>
/// Classifies an idempotency key against the accounts a command is about to post to
/// (ADR-0007). The primary account (customer, or transfer source) is checked first; the
/// counterpart (settlement, or transfer destination) is loaded later and checked too.
/// </summary>
public static class IdempotencyGuard
{
    /// <summary>
    /// Returns true when the request is a replay of a transaction already applied to
    /// <paramref name="primary"/>; throws when the key was used for a different request.
    /// </summary>
    public static bool IsReplay(Account primary, Guid transactionId, string fingerprint) =>
        primary.CheckIdempotency(transactionId, fingerprint) switch
        {
            IdempotencyCheck.NotApplied => false,
            IdempotencyCheck.Replay => true,
            IdempotencyCheck.PayloadMismatch => throw new IdempotencyKeyReusedException(transactionId),
            _ => throw new InvalidOperationException("Unknown idempotency check result."),
        };

    /// <summary>
    /// Called after the counterpart account is loaded, when <paramref name="primary"/> did
    /// not know the transaction.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>
    /// Same fingerprint on the counterpart: the identical request (the fingerprint includes
    /// both account ids) committed between our two reads. Both streams are appended
    /// atomically, so the primary's stream has moved on and our copy is stale. Raising a
    /// concurrency conflict lets <c>ConcurrencyRetryBehavior</c> reload and answer as a replay,
    /// instead of tripping the domain's duplicate-posting guard with a 422.
    /// </item>
    /// <item>
    /// Different fingerprint: the key was already used for another request that touched the
    /// counterpart (for example a deposit to a different customer), so it is rejected.
    /// </item>
    /// </list>
    /// </remarks>
    public static void EnsureCounterpartIsConsistent(Account primary, Account counterpart, Guid transactionId, string fingerprint)
    {
        switch (counterpart.CheckIdempotency(transactionId, fingerprint))
        {
            case IdempotencyCheck.Replay:
                throw new ConcurrencyException(primary.Id, primary.Version, primary.Version + 1);
            case IdempotencyCheck.PayloadMismatch:
                throw new IdempotencyKeyReusedException(transactionId);
        }
    }
}
