using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Domain.Ledger;

/// <summary>
/// Chart-of-accounts rules for each business operation. Customer accounts are
/// liabilities and the settlement account is an asset, so:
/// <list type="bullet">
/// <item>Deposit: debit settlement (cash in), credit customer (we owe more).</item>
/// <item>Withdrawal: debit customer (we owe less), credit settlement (cash out).</item>
/// <item>Transfer: debit source customer, credit destination customer.</item>
/// </list>
/// </summary>
public static class PostingRules
{
    public static Transaction Deposit(
        Guid transactionId,
        Guid customerAccountId,
        Guid settlementAccountId,
        Money amount,
        string description,
        DateTimeOffset occurredAt) =>
        Transaction.Create(
            transactionId,
            TransactionKind.Deposit,
            description,
            [
                new LedgerEntry(settlementAccountId, EntryType.Debit, amount),
                new LedgerEntry(customerAccountId, EntryType.Credit, amount),
            ],
            occurredAt);

    public static Transaction Withdrawal(
        Guid transactionId,
        Guid customerAccountId,
        Guid settlementAccountId,
        Money amount,
        string description,
        DateTimeOffset occurredAt) =>
        Transaction.Create(
            transactionId,
            TransactionKind.Withdrawal,
            description,
            [
                new LedgerEntry(customerAccountId, EntryType.Debit, amount),
                new LedgerEntry(settlementAccountId, EntryType.Credit, amount),
            ],
            occurredAt);

    public static Transaction Transfer(
        Guid transactionId,
        Guid sourceAccountId,
        Guid destinationAccountId,
        Money amount,
        string description,
        DateTimeOffset occurredAt) =>
        Transaction.Create(
            transactionId,
            TransactionKind.Transfer,
            description,
            [
                new LedgerEntry(sourceAccountId, EntryType.Debit, amount),
                new LedgerEntry(destinationAccountId, EntryType.Credit, amount),
            ],
            occurredAt);
}
