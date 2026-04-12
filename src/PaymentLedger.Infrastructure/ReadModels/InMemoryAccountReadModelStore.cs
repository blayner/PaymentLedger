using System.Collections.Concurrent;
using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.ReadModels;

namespace PaymentLedger.Infrastructure.ReadModels;

public sealed class InMemoryAccountReadModelStore : IAccountReadModelStore
{
    private readonly ConcurrentDictionary<Guid, AccountReadModel> _accounts = new();

    public Task<AccountReadModel?> GetAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.TryGetValue(accountId, out var model) ? model : null);

    public Task<PagedResult<AccountReadModel>> ListAsync(
        int page,
        int pageSize,
        bool includeSystemAccounts,
        CancellationToken cancellationToken)
    {
        var filtered = _accounts.Values
            .Where(a => includeSystemAccounts || !a.IsSystemAccount)
            .OrderBy(a => a.OpenedAt)
            .ThenBy(a => a.Id)
            .ToArray();

        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return Task.FromResult(new PagedResult<AccountReadModel>(items, page, pageSize, filtered.Length));
    }

    public Task UpsertAsync(AccountReadModel model, CancellationToken cancellationToken)
    {
        _accounts[model.Id] = model;
        return Task.CompletedTask;
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        _accounts.Clear();
        return Task.CompletedTask;
    }
}
