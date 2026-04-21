using PaymentLedger.Domain.Common;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Domain.Ledger;

/// <summary>
/// A balanced journal entry. It cannot be constructed unless the sum of its debits
/// equals the sum of its credits, so an unbalanced posting is unrepresentable rather
/// than merely invalid.
/// </summary>
public sealed class Transaction
{
    private Transaction(
        Guid id,
        TransactionKind kind,
        string description,
        DateTimeOffset occurredAt,
        IReadOnlyList<LedgerEntry> entries,
        Money total)
    {
        Id = id;
        Kind = kind;
        Description = description;
        OccurredAt = occurredAt;
        Entries = entries;
        Total = total;
    }

    public Guid Id { get; }

    public TransactionKind Kind { get; }

    public string Description { get; }

    public DateTimeOffset OccurredAt { get; }

    public IReadOnlyList<LedgerEntry> Entries { get; }

    /// <summary>Sum of the debit side (equal to the sum of the credit side).</summary>
    public Money Total { get; }

    public string Currency => Total.Currency;

    public static Transaction Create(
        Guid id,
        TransactionKind kind,
        string description,
        IEnumerable<LedgerEntry> entries,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (id == Guid.Empty)
        {
            throw new DomainException("transaction.invalid_id", "Transaction id must not be empty.");
        }

        var list = entries.ToArray();

        if (list.Length < 2)
        {
            throw new DomainException("transaction.too_few_entries", "A transaction needs at least one debit and one credit.");
        }

        var currency = list[0].Amount.Currency;
        if (list.Any(e => e.Amount.Currency != currency))
        {
            throw new DomainException("transaction.mixed_currencies", "All entries of a transaction must use the same currency.");
        }

        if (list.Select(e => e.AccountId).Distinct().Count() != list.Length)
        {
            throw new DomainException("transaction.duplicate_account", "An account may appear only once per transaction.");
        }

        var debits = Sum(list, EntryType.Debit, currency);
        var credits = Sum(list, EntryType.Credit, currency);

        if (debits != credits)
        {
            throw new DomainException(
                "transaction.unbalanced",
                $"Debits ({debits}) must equal credits ({credits}).");
        }

        return new Transaction(
            id,
            kind,
            string.IsNullOrWhiteSpace(description) ? kind.ToString() : description.Trim(),
            occurredAt,
            list,
            debits);
    }

    private static Money Sum(IEnumerable<LedgerEntry> entries, EntryType type, string currency) =>
        entries.Where(e => e.Type == type).Aggregate(Money.Zero(currency), (total, e) => total + e.Amount);
}
