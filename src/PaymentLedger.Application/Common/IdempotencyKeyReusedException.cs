namespace PaymentLedger.Application.Common;

/// <summary>
/// Raised when an idempotency key that already produced a transaction arrives with a
/// different payload. Returning the original result would hide a client bug (or worse,
/// tell the client a different movement succeeded), so the request is rejected.
/// </summary>
public sealed class IdempotencyKeyReusedException(Guid idempotencyKey)
    : Exception($"Idempotency key {idempotencyKey} was already used for a different request.")
{
    public Guid IdempotencyKey { get; } = idempotencyKey;
}
