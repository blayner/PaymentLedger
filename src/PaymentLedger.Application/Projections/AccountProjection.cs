using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.ReadModels;
using PaymentLedger.Domain.Abstractions;
using PaymentLedger.Domain.Accounts;
using PaymentLedger.Domain.Accounts.Events;

namespace PaymentLedger.Application.Projections;

public sealed class AccountProjection(IAccountReadModelStore store) : IProjection
{
    public async Task ApplyAsync(StoredEvent storedEvent, CancellationToken cancellationToken)
    {
        if (storedEvent.Event is AccountCreatedEvent created)
        {
            await store.UpsertAsync(
                new AccountReadModel(
                    created.AccountId,
                    created.OwnerName,
                    created.Currency,
                    created.AccountType,
                    AccountStatus.Active,
                    created.IsSystemAccount,
                    Balance: 0m,
                    storedEvent.Version,
                    TransactionCount: 0,
                    created.OccurredAt,
                    created.OccurredAt),
                cancellationToken);
            return;
        }

        var current = await store.GetAsync(storedEvent.StreamId, cancellationToken);
        if (current is null || storedEvent.Version <= current.Version)
        {
            return;
        }

        var updated = storedEvent.Event switch
        {
            BalanceChangedEvent e => current with
            {
                Balance = e.BalanceAfter,
                TransactionCount = current.TransactionCount + 1,
            },
            AccountFrozenEvent => current with { Status = AccountStatus.Frozen },
            AccountUnfrozenEvent => current with { Status = AccountStatus.Active },
            AccountClosedEvent => current with { Status = AccountStatus.Closed },
            _ => current,
        };

        await store.UpsertAsync(
            updated with { Version = storedEvent.Version, LastActivityAt = storedEvent.Event.OccurredAt },
            cancellationToken);
    }

    public Task ResetAsync(CancellationToken cancellationToken) => store.ResetAsync(cancellationToken);
}
