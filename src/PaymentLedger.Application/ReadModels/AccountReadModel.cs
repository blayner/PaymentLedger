using PaymentLedger.Domain.Accounts;

namespace PaymentLedger.Application.ReadModels;

/// <summary>Denormalised, query-optimised view of an account. Replaced (never mutated) by projections.</summary>
public sealed record AccountReadModel(
    Guid Id,
    string OwnerName,
    string Currency,
    AccountType Type,
    AccountStatus Status,
    bool IsSystemAccount,
    decimal Balance,
    long Version,
    int TransactionCount,
    DateTimeOffset OpenedAt,
    DateTimeOffset LastActivityAt);
