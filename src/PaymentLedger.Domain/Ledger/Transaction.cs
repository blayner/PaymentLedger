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
        Money total,
        string? requestFingerprint)
    {
        Id = id;
        Kind = kind;
        Description = description;
        OccurredAt = occurredAt;
        Entries = entries;
        Total = total;
        RequestFingerprint = requestFingerprint;
    }

    public Guid Id { get; }

    public TransactionKind Kind { get; }

    public string Description { get; }

    public DateTimeOffset OccurredAt { get; }

    public IReadOnlyList<LedgerEntry> Entries { get; }

    /// <summary>Sum of the debit side (equal to the sum of the credit side).</summary>
    public Money Total { get; }

    public string Currency => Total.Currency;

    /// <summary>
    /// SHA-256 of the canonical request that produced this transaction. Stored with the
    /// balance-changed events so a reused idempotency key can be told apart from a replay.
    /// Null for transactions created without a client request (and for legacy events).
    /// </summary>
    public string? RequestFingerprint { get; }

    public static Transaction Create(
        Guid id,
        TransactionKind kind,
        string description,
        IEnumerable<LedgerEntry> entries,
        DateTimeOffset occurredAt,
        string? requestFingerprint = null)
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
            debits,
            string.IsNullOrWhiteSpace(requestFingerprint) ? null : requestFingerprint);
    }

    private static Money Sum(IEnumerable<LedgerEntry> entries, EntryType type, string currency) =>
        entries.Where(e => e.Type == type).Aggregate(Money.Zero(currency), (total, e) => total + e.Amount);
}
