using PaymentLedger.Application.Abstractions;
using PaymentLedger.Domain.Abstractions;
using PaymentLedger.Domain.Accounts;

namespace PaymentLedger.Infrastructure.Persistence;

public sealed class EventSourcedAccountRepository(IEventStore eventStore) : IAccountRepository
{
    public async Task<Account?> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var events = await eventStore.ReadStreamAsync(accountId, cancellationToken).ConfigureAwait(false);
        return events.Count == 0 ? null : Account.Rehydrate(events.Select(e => e.Event));
    }

    public async Task SaveAsync(IReadOnlyCollection<Account> accounts, CancellationToken cancellationToken)
    {
        var appends = accounts
            .Where(a => a.UncommittedEvents.Count > 0)
            .Select(a => new StreamAppend(a.Id, a.Version, a.UncommittedEvents.ToArray()))
            .ToArray();

        if (appends.Length == 0)
        {
            return;
        }

        await eventStore.AppendAsync(appends, cancellationToken).ConfigureAwait(false);

        foreach (var account in accounts)
        {
            account.MarkCommitted();
        }
    }
}
