namespace PaymentLedger.Domain.Ledger;

/// <summary>
/// A financial transaction: a set of ledger entries that are recorded together.
/// </summary>
public sealed record Transaction(
    Guid Id,
    string Description,
    DateTimeOffset OccurredAt,
    IReadOnlyList<LedgerEntry> Entries);
