using PaymentLedger.Domain.Accounts;

namespace PaymentLedger.Application.Abstractions;

public interface IAccountRepository
{
    Task<Account?> GetAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the uncommitted events of every given account in a single atomic append.
    /// Operations that touch several accounts (transfers, double-entry postings) rely on this.
    /// </summary>
    Task SaveAsync(IReadOnlyCollection<Account> accounts, CancellationToken cancellationToken);
}
