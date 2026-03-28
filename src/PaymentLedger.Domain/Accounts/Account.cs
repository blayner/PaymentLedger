using PaymentLedger.Domain.Accounts.Events;
using PaymentLedger.Domain.Common;
using PaymentLedger.Domain.Ledger;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Domain.Accounts;

/// <summary>
/// Event-sourced account aggregate. The current balance is a left fold over the
/// account's event stream; nothing is stored except the events themselves.
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

    public Money Balance { get; private set; } = null!;

    public DateTimeOffset OpenedAt { get; private set; }

    public static Account Open(Guid id, string ownerName, string currency)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("account.invalid_id", "Account id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(ownerName))
        {
            throw new DomainException("account.invalid_owner", "Owner name is required.");
        }

        // Validates the currency code before any event is raised.
        _ = Money.Zero(currency);

        var account = new Account();
        account.Raise(new AccountCreatedEvent(id, ownerName.Trim(), currency, AccountType.Liability, IsSystemAccount: false));
        return account;
    }

    public static Account Rehydrate(IEnumerable<IDomainEvent> history)
    {
        var account = new Account();
        account.LoadFromHistory(history);
        return account;
    }

    /// <summary>True when the transaction has already moved this account's balance (idempotency check).</summary>
    public bool HasApplied(Guid transactionId) => _appliedTransactions.Contains(transactionId);

    public void Deposit(Money amount, Guid transactionId, string description)
    {
        var newBalance = PrepareChange(amount, transactionId, increase: true);
        Raise(new FundsDepositedEvent(Id, transactionId, Type.NormalSide(), amount.Amount, Currency, newBalance.Amount, description));
    }

    public void Withdraw(Money amount, Guid transactionId, string description)
    {
        var newBalance = PrepareChange(amount, transactionId, increase: false);
        Raise(new FundsWithdrawnEvent(Id, transactionId, Opposite(Type.NormalSide()), amount.Amount, Currency, newBalance.Amount, description));
    }

    public void TransferOut(Money amount, Guid transactionId, Guid destinationAccountId, string description)
    {
        var newBalance = PrepareChange(amount, transactionId, increase: false);
        Raise(new TransferInitiatedEvent(Id, transactionId, Opposite(Type.NormalSide()), amount.Amount, Currency, newBalance.Amount, description, destinationAccountId));
    }

    public void ReceiveTransfer(Money amount, Guid transactionId, Guid sourceAccountId, string description)
    {
        var newBalance = PrepareChange(amount, transactionId, increase: true);
        Raise(new TransferReceivedEvent(Id, transactionId, Type.NormalSide(), amount.Amount, Currency, newBalance.Amount, description, sourceAccountId));
    }

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

    protected override void When(IDomainEvent @event)
    {
        switch (@event)
        {
            case AccountCreatedEvent e:
                Id = e.AccountId;
                OwnerName = e.OwnerName;
                Currency = e.Currency;
                Type = e.AccountType;
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

    private static EntryType Opposite(EntryType type) =>
        type == EntryType.Debit ? EntryType.Credit : EntryType.Debit;

    private Money PrepareChange(Money amount, Guid transactionId, bool increase)
    {
        EnsureStatus(AccountStatus.Active);

        if (transactionId == Guid.Empty)
        {
            throw new DomainException("transaction.invalid_id", "Transaction id must not be empty.");
        }

        if (HasApplied(transactionId))
        {
            throw new DomainException("transaction.duplicate", $"Transaction {transactionId} was already applied to account {Id}.");
        }

        if (!amount.IsPositive)
        {
            throw new DomainException("money.non_positive", "Amount must be greater than zero.");
        }

        var newBalance = increase ? Balance + amount : Balance - amount;

        if (newBalance.IsNegative)
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
