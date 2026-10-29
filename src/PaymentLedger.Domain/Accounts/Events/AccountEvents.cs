using PaymentLedger.Domain.Common;
using PaymentLedger.Domain.Ledger;

namespace PaymentLedger.Domain.Accounts.Events;

public sealed record AccountCreatedEvent(
    Guid AccountId,
    string OwnerName,
    string Currency,
    AccountType AccountType,
    bool IsSystemAccount) : DomainEvent;

/// <summary>
/// Common shape of every event that moves an account's balance. Each one records the
/// ledger side it was posted on and the resulting balance, so read models can be built
/// without re-deriving accounting rules.
/// </summary>
public abstract record BalanceChangedEvent(
    Guid AccountId,
    Guid TransactionId,
    EntryType EntryType,
    decimal Amount,
    string Currency,
    decimal BalanceAfter,
    string Description) : DomainEvent
{
    /// <summary>
    /// Fingerprint of the client request behind the transaction (see ADR-0007).
    /// Optional so events written before it existed still deserialize; null means
    /// "unknown", and such transactions are treated as plain replays.
    /// </summary>
    public string? RequestFingerprint { get; init; }
}

public sealed record FundsDepositedEvent(
    Guid AccountId,
    Guid TransactionId,
    EntryType EntryType,
    decimal Amount,
    string Currency,
    decimal BalanceAfter,
    string Description)
    : BalanceChangedEvent(AccountId, TransactionId, EntryType, Amount, Currency, BalanceAfter, Description);

public sealed record FundsWithdrawnEvent(
    Guid AccountId,
    Guid TransactionId,
    EntryType EntryType,
    decimal Amount,
    string Currency,
    decimal BalanceAfter,
    string Description)
    : BalanceChangedEvent(AccountId, TransactionId, EntryType, Amount, Currency, BalanceAfter, Description);

public sealed record TransferInitiatedEvent(
    Guid AccountId,
    Guid TransactionId,
    EntryType EntryType,
    decimal Amount,
    string Currency,
    decimal BalanceAfter,
    string Description,
    Guid DestinationAccountId)
    : BalanceChangedEvent(AccountId, TransactionId, EntryType, Amount, Currency, BalanceAfter, Description);

public sealed record TransferReceivedEvent(
    Guid AccountId,
    Guid TransactionId,
    EntryType EntryType,
    decimal Amount,
    string Currency,
    decimal BalanceAfter,
    string Description,
    Guid SourceAccountId)
    : BalanceChangedEvent(AccountId, TransactionId, EntryType, Amount, Currency, BalanceAfter, Description);

public sealed record AccountFrozenEvent(Guid AccountId, string Reason) : DomainEvent;

public sealed record AccountUnfrozenEvent(Guid AccountId) : DomainEvent;

public sealed record AccountClosedEvent(Guid AccountId) : DomainEvent;
