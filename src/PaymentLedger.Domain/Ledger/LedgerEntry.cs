using PaymentLedger.Domain.Common;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Domain.Ledger;

/// <summary>One side of a double-entry posting against a single account.</summary>
public sealed record LedgerEntry
{
    public LedgerEntry(Guid accountId, EntryType type, Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (accountId == Guid.Empty)
        {
            throw new DomainException("entry.invalid_account", "Ledger entry must reference an account.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainException("entry.invalid_type", $"Unknown entry type {type}.");
        }

        // Direction is expressed by Debit/Credit, never by sign.
        if (!amount.IsPositive)
        {
            throw new DomainException("entry.non_positive", "Ledger entry amounts must be greater than zero.");
        }

        AccountId = accountId;
        Type = type;
        Amount = amount;
    }

    public Guid AccountId { get; }

    public EntryType Type { get; }

    public Money Amount { get; }
}
