using PaymentLedger.Application.ReadModels;

namespace PaymentLedger.Application.Contracts;

public sealed record AccountDto(
    Guid Id,
    string OwnerName,
    string Currency,
    string Type,
    string Status,
    bool IsSystemAccount,
    decimal Balance,
    int TransactionCount,
    DateTimeOffset OpenedAt,
    DateTimeOffset LastActivityAt)
{
    public static AccountDto From(AccountReadModel model) => new(
        model.Id,
        model.OwnerName,
        model.Currency,
        model.Type.ToString(),
        model.Status.ToString(),
        model.IsSystemAccount,
        model.Balance,
        model.TransactionCount,
        model.OpenedAt,
        model.LastActivityAt);
}

/// <param name="Version">Stream version the balance was computed at; useful for optimistic UI updates.</param>
public sealed record AccountBalanceDto(Guid AccountId, decimal Balance, string Currency, long Version, DateTimeOffset AsOf);

public sealed record LedgerEntryDto(
    long Position,
    Guid TransactionId,
    Guid AccountId,
    string Kind,
    string EntryType,
    decimal Amount,
    string Currency,
    decimal BalanceAfter,
    string Description,
    Guid? CounterpartyAccountId,
    DateTimeOffset OccurredAt)
{
    public static LedgerEntryDto From(LedgerEntryReadModel model) => new(
        model.Position,
        model.TransactionId,
        model.AccountId,
        model.Kind,
        model.EntryType.ToString(),
        model.Amount,
        model.Currency,
        model.BalanceAfter,
        model.Description,
        model.CounterpartyAccountId,
        model.OccurredAt);
}
