using PaymentLedger.Domain.Ledger;

namespace PaymentLedger.Domain.Accounts;

public static class AccountTypeExtensions
{
    /// <summary>The ledger side that increases the balance of an account of this type.</summary>
    public static EntryType NormalSide(this AccountType type) => type switch
    {
        AccountType.Asset => EntryType.Debit,
        AccountType.Liability => EntryType.Credit,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown account type."),
    };

    /// <summary>+1 when an entry of <paramref name="entryType"/> increases the balance, -1 when it decreases it.</summary>
    public static int BalanceSign(this AccountType type, EntryType entryType) =>
        type.NormalSide() == entryType ? 1 : -1;
}
