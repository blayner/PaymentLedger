using PaymentLedger.Application.Abstractions;
using PaymentLedger.Domain.Accounts;

namespace PaymentLedger.Application.Common;

internal static class AccountRepositoryExtensions
{
    public static async Task<Account> GetRequiredAsync(
        this IAccountRepository repository,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        return await repository.GetAsync(accountId, cancellationToken).ConfigureAwait(false)
               ?? throw new NotFoundException("Account", accountId);
    }

    /// <summary>
    /// Loads the settlement account for <paramref name="currency"/>, opening it on first use.
    /// If two requests open it concurrently, the second append fails on the expected version
    /// and the command is retried by <c>ConcurrencyRetryBehavior</c>.
    /// </summary>
    public static async Task<Account> GetOrOpenSettlementAsync(
        this IAccountRepository repository,
        string currency,
        CancellationToken cancellationToken)
    {
        var id = SystemAccounts.SettlementAccountId(currency);
        return await repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
               ?? Account.OpenSystem(id, SystemAccounts.SettlementAccountName(currency), currency);
    }
}
