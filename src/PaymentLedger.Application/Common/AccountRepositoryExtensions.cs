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
}
