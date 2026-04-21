using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.ReadModels;
using PaymentLedger.Domain.Ledger;

namespace PaymentLedger.Infrastructure.ReadModels;

public sealed class InMemoryLedgerReadModelStore : ILedgerReadModelStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, List<LedgerEntryReadModel>> _byAccount = [];
    private readonly Dictionary<Guid, List<LedgerEntryReadModel>> _byTransaction = [];

    public Task AppendAsync(LedgerEntryReadModel entry, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            Index(_byAccount, entry.AccountId, entry);
            Index(_byTransaction, entry.TransactionId, entry);
        }

        return Task.CompletedTask;
    }

    public Task<PagedResult<LedgerEntryReadModel>> GetByAccountAsync(
        Guid accountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_byAccount.TryGetValue(accountId, out var entries))
            {
                return Task.FromResult(new PagedResult<LedgerEntryReadModel>([], page, pageSize, 0));
            }

            // Entries are appended in log order, so reversing yields newest first.
            var items = Enumerable.Reverse(entries).Skip((page - 1) * pageSize).Take(pageSize).ToArray();
            return Task.FromResult(new PagedResult<LedgerEntryReadModel>(items, page, pageSize, entries.Count));
        }
    }

    public Task<IReadOnlyList<LedgerEntryReadModel>> GetByTransactionAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<LedgerEntryReadModel> result = _byTransaction.TryGetValue(transactionId, out var entries)
                ? entries.ToArray()
                : [];
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<LedgerTotals>> GetTotalsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<LedgerTotals> totals = _byTransaction.Values
                .SelectMany(entries => entries)
                .GroupBy(e => e.Currency, StringComparer.Ordinal)
                .Select(g => new LedgerTotals(
                    g.Key,
                    g.Where(e => e.EntryType == EntryType.Debit).Sum(e => e.Amount),
                    g.Where(e => e.EntryType == EntryType.Credit).Sum(e => e.Amount),
                    g.LongCount()))
                .ToArray();
            return Task.FromResult(totals);
        }
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _byAccount.Clear();
            _byTransaction.Clear();
        }

        return Task.CompletedTask;
    }

    private static void Index(Dictionary<Guid, List<LedgerEntryReadModel>> index, Guid key, LedgerEntryReadModel entry)
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = [];
            index[key] = list;
        }

        list.Add(entry);
    }
}
