namespace PaymentLedger.Application.Contracts;

/// <param name="AlreadyProcessed">
/// True when the transaction id had already been applied and the request was a safe retry.
/// </param>
public sealed record TransactionResult(
    Guid TransactionId,
    Guid AccountId,
    decimal Balance,
    string Currency,
    bool AlreadyProcessed);
