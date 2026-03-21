using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Domain.Ledger;

/// <summary>One side of a double-entry posting against a single account.</summary>
public sealed record LedgerEntry(Guid AccountId, EntryType Type, Money Amount);
