namespace PaymentLedger.Api.Contracts;

public sealed record CreateAccountRequest(string OwnerName, string Currency);

public sealed record MoneyMovementRequest(decimal Amount, string Currency, string? Description);

public sealed record TransferRequest(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency,
    string? Description);

public sealed record FreezeAccountRequest(string Reason);
