using System.Security.Cryptography;
using System.Text;

namespace PaymentLedger.Domain.Accounts;

/// <summary>
/// Well-known internal accounts. Ids are derived deterministically from the currency
/// so every instance resolves the same settlement account without a lookup table.
/// </summary>
public static class SystemAccounts
{
    public static Guid SettlementAccountId(string currency) => NameBasedGuid($"payment-ledger:settlement:{currency}");

    public static string SettlementAccountName(string currency) => $"Settlement {currency}";

    private static Guid NameBasedGuid(string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        var bytes = hash[..16];

        // Mark as a name-based (version 5 style), RFC 4122 variant identifier.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
