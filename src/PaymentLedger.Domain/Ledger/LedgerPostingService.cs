using PaymentLedger.Domain.Accounts;
using PaymentLedger.Domain.Common;

namespace PaymentLedger.Domain.Ledger;

/// <summary>
/// Applies a balanced <see cref="Transaction"/> to the accounts it touches.
/// Every entry is validated against its account before any account is changed,
/// so a posting either lands completely or not at all.
/// </summary>
public sealed class LedgerPostingService
{
    public void Post(Transaction transaction, IReadOnlyCollection<Account> accounts)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(accounts);

        var byId = accounts.ToDictionary(a => a.Id);

        foreach (var entry in transaction.Entries)
        {
            if (!byId.ContainsKey(entry.AccountId))
            {
                throw new DomainException(
                    "posting.missing_account",
                    $"Account {entry.AccountId} referenced by transaction {transaction.Id} was not supplied.");
            }
        }

        foreach (var entry in transaction.Entries)
        {
            byId[entry.AccountId].EnsureCanPost(entry, transaction.Id);
        }

        foreach (var entry in transaction.Entries)
        {
            byId[entry.AccountId].Post(entry, transaction);
        }
    }
}
