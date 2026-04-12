using PaymentLedger.Domain.Ledger;

namespace PaymentLedger.Application.ReadModels;

public sealed record LedgerEntryReadModel(
    long Position,
    Guid TransactionId,
    Guid AccountId,
    string Kind,
    EntryType EntryType,
    decimal Amount,
    string Currency,
    decimal BalanceAfter,
    string Description,
    Guid? CounterpartyAccountId,
    DateTimeOffset OccurredAt);
