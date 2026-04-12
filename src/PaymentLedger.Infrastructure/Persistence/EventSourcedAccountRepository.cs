using Microsoft.Extensions.Logging;
using PaymentLedger.Application.Abstractions;
using PaymentLedger.Domain.Abstractions;
using PaymentLedger.Domain.Accounts;

namespace PaymentLedger.Infrastructure.Persistence;

public sealed class EventSourcedAccountRepository(
    IEventStore eventStore,
    IProjectionRunner projections,
    ILogger<EventSourcedAccountRepository> logger) : IAccountRepository
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

        // Read-your-writes for this instance: project before the command returns.
        // The write is already durable, so a projection failure must not fail the command;
        // the background poller will pick the events up on its next tick.
        try
        {
            await projections.CatchUpAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Inline projection failed after a successful append");
        }
    }
}
