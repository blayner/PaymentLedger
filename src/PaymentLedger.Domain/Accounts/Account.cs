using PaymentLedger.Domain.Accounts.Events;
using PaymentLedger.Domain.Common;
using PaymentLedger.Domain.Ledger;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Domain.Accounts;

/// <summary>
/// Event-sourced account aggregate. The current balance is a left fold over the
/// account's event stream; nothing is stored except the events themselves.
/// Balances only change through <see cref="LedgerPostingService"/>, which guarantees
/// every movement is one side of a balanced double-entry transaction.
/// </summary>
public sealed class Account : AggregateRoot
{
    private readonly HashSet<Guid> _appliedTransactions = [];

    private Account()
    {
    }

    public string OwnerName { get; private set; } = string.Empty;

    public string Currency { get; private set; } = string.Empty;

    public AccountType Type { get; private set; }

    public AccountStatus Status { get; private set; }

    public bool IsSystemAccount { get; private set; }

    public Money Balance { get; private set; } = null!;

    public DateTimeOffset OpenedAt { get; private set; }

    /// <summary>Opens a customer wallet (a liability of the platform).</summary>
    public static Account Open(Guid id, string ownerName, string currency) =>
        Create(id, ownerName, currency, AccountType.Liability, isSystemAccount: false);

    /// <summary>Opens an internal account such as a settlement account (an asset of the platform).</summary>
    public static Account OpenSystem(Guid id, string name, string currency) =>
        Create(id, name, currency, AccountType.Asset, isSystemAccount: true);

    public static Account Rehydrate(IEnumerable<IDomainEvent> history)
    {
        var account = new Account();
        account.LoadFromHistory(history);
        return account;
    }

    /// <summary>True when the transaction has already moved this account's balance (idempotency check).</summary>
    public bool HasApplied(Guid transactionId) => _appliedTransactions.Contains(transactionId);

    public void Freeze(string reason)
    {
        EnsureStatus(AccountStatus.Active);
        Raise(new AccountFrozenEvent(Id, string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason.Trim()));
    }

    public void Unfreeze()
    {
        EnsureStatus(AccountStatus.Frozen);
        Raise(new AccountUnfrozenEvent(Id));
    }

    public void Close()
    {
        if (IsSystemAccount)
        {
            throw new DomainException("account.system_account", "System accounts cannot be closed.");
        }

        if (Status == AccountStatus.Closed)
        {
            throw new DomainException("account.already_closed", $"Account {Id} is already closed.");
        }

        if (!Balance.IsZero)
        {
            throw new DomainException("account.non_zero_balance", "Only accounts with zero balance can be closed.");
        }

        Raise(new AccountClosedEvent(Id));
    }

    internal void EnsureCanPost(LedgerEntry entry, Guid transactionId) => _ = ComputeBalanceAfter(entry, transactionId);

    internal void Post(LedgerEntry entry, Transaction transaction)
    {
        var newBalance = ComputeBalanceAfter(entry, transaction.Id);
        var increases = Type.BalanceSign(entry.Type) > 0;
        var counterparty = transaction.Entries.First(e => e.AccountId != Id).AccountId;
        var amount = entry.Amount.Amount;

        IDomainEvent @event = (transaction.Kind, increases) switch
        {
            (TransactionKind.Transfer, false) => new TransferInitiatedEvent(
                Id, transaction.Id, entry.Type, amount, Currency, newBalance.Amount, transaction.Description, counterparty),
            (TransactionKind.Transfer, true) => new TransferReceivedEvent(
                Id, transaction.Id, entry.Type, amount, Currency, newBalance.Amount, transaction.Description, counterparty),
            (_, true) => new FundsDepositedEvent(
                Id, transaction.Id, entry.Type, amount, Currency, newBalance.Amount, transaction.Description),
            (_, false) => new FundsWithdrawnEvent(
                Id, transaction.Id, entry.Type, amount, Currency, newBalance.Amount, transaction.Description),
        };

        Raise(@event);
    }

    protected override void When(IDomainEvent @event)
    {
        switch (@event)
        {
            case AccountCreatedEvent e:
                Id = e.AccountId;
                OwnerName = e.OwnerName;
                Currency = e.Currency;
                Type = e.AccountType;
                IsSystemAccount = e.IsSystemAccount;
                Balance = Money.Zero(e.Currency);
                Status = AccountStatus.Active;
                OpenedAt = e.OccurredAt;
                break;
            case BalanceChangedEvent e:
                Balance = new Money(e.BalanceAfter, Currency);
                _appliedTransactions.Add(e.TransactionId);
                break;
            case AccountFrozenEvent:
                Status = AccountStatus.Frozen;
                break;
            case AccountUnfrozenEvent:
                Status = AccountStatus.Active;
                break;
            case AccountClosedEvent:
                Status = AccountStatus.Closed;
                break;
            default:
                throw new InvalidOperationException($"Account cannot apply event {@event.GetType().Name}.");
        }
    }

    private static Account Create(Guid id, string name, string currency, AccountType type, bool isSystemAccount)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("account.invalid_id", "Account id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("account.invalid_owner", "Owner name is required.");
        }

        // Validates the currency code before any event is raised.
        _ = Money.Zero(currency);

        var account = new Account();
        account.Raise(new AccountCreatedEvent(id, name.Trim(), currency, type, isSystemAccount));
        return account;
    }

    private Money ComputeBalanceAfter(LedgerEntry entry, Guid transactionId)
    {
        if (entry.AccountId != Id)
        {
            throw new DomainException("posting.wrong_account", $"Entry for {entry.AccountId} cannot be posted to {Id}.");
        }

        EnsureStatus(AccountStatus.Active);

        if (entry.Amount.Currency != Currency)
        {
            throw new DomainException(
                "posting.currency_mismatch",
                $"Account {Id} holds {Currency}; cannot post {entry.Amount.Currency}.");
        }

        if (HasApplied(transactionId))
        {
            throw new DomainException("transaction.duplicate", $"Transaction {transactionId} was already applied to account {Id}.");
        }

        var newBalance = Type.BalanceSign(entry.Type) > 0
            ? Balance + entry.Amount
            : Balance - entry.Amount;

        // Customer wallets cannot be overdrawn. System accounts mirror external positions
        // and are allowed to go negative (e.g. settlement in transit).
        if (newBalance.IsNegative && !IsSystemAccount)
        {
            throw new DomainException("account.insufficient_funds", $"Account {Id} has insufficient funds.");
        }

        return newBalance;
    }

    private void EnsureStatus(AccountStatus expected)
    {
        if (Status != expected)
        {
            throw new DomainException("account.invalid_status", $"Account {Id} is {Status}; expected {expected}.");
        }
    }
}
