using PaymentLedger.Application.Common;
using PaymentLedger.Application.ReadModels;

namespace PaymentLedger.Application.Abstractions;

public interface IAccountReadModelStore
{
    Task<AccountReadModel?> GetAsync(Guid accountId, CancellationToken cancellationToken);

    Task<PagedResult<AccountReadModel>> ListAsync(int page, int pageSize, bool includeSystemAccounts, CancellationToken cancellationToken);

    Task UpsertAsync(AccountReadModel model, CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);
}

public interface ILedgerReadModelStore
{
    Task AppendAsync(LedgerEntryReadModel entry, CancellationToken cancellationToken);

    /// <summary>Entries for one account, newest first.</summary>
    Task<PagedResult<LedgerEntryReadModel>> GetByAccountAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<LedgerEntryReadModel>> GetByTransactionAsync(Guid transactionId, CancellationToken cancellationToken);

    /// <summary>Total debits and credits per currency across the whole ledger.</summary>
    Task<IReadOnlyList<LedgerTotals>> GetTotalsAsync(CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);
}

public sealed record LedgerTotals(string Currency, decimal TotalDebits, decimal TotalCredits, long EntryCount);
