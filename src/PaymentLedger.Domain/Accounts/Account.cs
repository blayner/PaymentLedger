using PaymentLedger.Domain.Common;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Domain.Accounts;

public sealed class Account
{
    public Account(Guid id, string ownerName, string currency, AccountType type = AccountType.Liability)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("account.invalid_id", "Account id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(ownerName))
        {
            throw new DomainException("account.invalid_owner", "Owner name is required.");
        }

        Id = id;
        OwnerName = ownerName.Trim();
        Type = type;
        Balance = Money.Zero(currency);
        Status = AccountStatus.Active;
        OpenedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }

    public string OwnerName { get; }

    public AccountType Type { get; }

    public AccountStatus Status { get; private set; }

    public Money Balance { get; private set; }

    public string Currency => Balance.Currency;

    public DateTimeOffset OpenedAt { get; }

    public void Deposit(Money amount)
    {
        EnsureActive();
        EnsurePositive(amount);
        Balance += amount;
    }

    public void Withdraw(Money amount)
    {
        EnsureActive();
        EnsurePositive(amount);

        if (Balance < amount)
        {
            throw new DomainException("account.insufficient_funds", $"Account {Id} has insufficient funds.");
        }

        Balance -= amount;
    }

    public void Freeze()
    {
        EnsureActive();
        Status = AccountStatus.Frozen;
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

        Status = AccountStatus.Closed;
    }

    private void EnsureActive()
    {
        if (Status != AccountStatus.Active)
        {
            throw new DomainException("account.not_active", $"Account {Id} is {Status}.");
        }
    }

    private void EnsurePositive(Money amount)
    {
        if (!amount.IsPositive)
        {
            throw new DomainException("money.non_positive", "Amount must be greater than zero.");
        }
    }
}
