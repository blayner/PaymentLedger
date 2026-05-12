using PaymentLedger.Domain.Common;
using PaymentLedger.Domain.Ledger;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Domain.Tests;

public sealed class TransactionTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    private static Money Brl(decimal amount) => new(amount, "BRL");

    private static string ErrorCode(Action act) => Should.Throw<DomainException>(act).Code;

    [Fact]
    public void Creates_a_balanced_transaction()
    {
        var tx = Transaction.Create(
            Guid.NewGuid(),
            TransactionKind.Transfer,
            "split",
            [
                new LedgerEntry(A, EntryType.Debit, Brl(100m)),
                new LedgerEntry(B, EntryType.Credit, Brl(60m)),
                new LedgerEntry(C, EntryType.Credit, Brl(40m)),
            ],
            Now);

        tx.Total.ShouldBe(Brl(100m));
        tx.Entries.Count.ShouldBe(3);
    }

    [Fact]
    public void Rejects_unbalanced_entries()
    {
        var code = ErrorCode(() => Transaction.Create(
            Guid.NewGuid(),
            TransactionKind.Transfer,
            "broken",
            [
                new LedgerEntry(A, EntryType.Debit, Brl(100m)),
                new LedgerEntry(B, EntryType.Credit, Brl(99.99m)),
            ],
            Now));

        code.ShouldBe("transaction.unbalanced");
    }

    [Fact]
    public void Rejects_a_single_entry()
    {
        var code = ErrorCode(() => Transaction.Create(
            Guid.NewGuid(),
            TransactionKind.Deposit,
            "lonely",
            [new LedgerEntry(A, EntryType.Debit, Brl(1m))],
            Now));

        code.ShouldBe("transaction.too_few_entries");
    }

    [Fact]
    public void Rejects_mixed_currencies()
    {
        var code = ErrorCode(() => Transaction.Create(
            Guid.NewGuid(),
            TransactionKind.Transfer,
            "fx",
            [
                new LedgerEntry(A, EntryType.Debit, Brl(10m)),
                new LedgerEntry(B, EntryType.Credit, new Money(10m, "USD")),
            ],
            Now));

        code.ShouldBe("transaction.mixed_currencies");
    }

    [Fact]
    public void Rejects_the_same_account_on_both_sides()
    {
        var code = ErrorCode(() => Transaction.Create(
            Guid.NewGuid(),
            TransactionKind.Transfer,
            "self",
            [
                new LedgerEntry(A, EntryType.Debit, Brl(10m)),
                new LedgerEntry(A, EntryType.Credit, Brl(10m)),
            ],
            Now));

        code.ShouldBe("transaction.duplicate_account");
    }

    [Fact]
    public void Ledger_entries_must_be_positive()
    {
        ErrorCode(() => new LedgerEntry(A, EntryType.Debit, Brl(0m))).ShouldBe("entry.non_positive");
    }

    [Fact]
    public void Posting_rules_debit_settlement_and_credit_customer_on_deposit()
    {
        var tx = PostingRules.Deposit(Guid.NewGuid(), customerAccountId: A, settlementAccountId: B, Brl(50m), "top-up", Now);

        tx.Entries.ShouldContain(e => e.AccountId == B && e.Type == EntryType.Debit, 1);
        tx.Entries.ShouldContain(e => e.AccountId == A && e.Type == EntryType.Credit, 1);
    }
}
