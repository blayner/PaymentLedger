using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.ReadModels;
using PaymentLedger.Domain.Abstractions;
using PaymentLedger.Domain.Accounts.Events;

namespace PaymentLedger.Application.Projections;

/// <summary>Turns every balance-changing event into a row of the general ledger.</summary>
public sealed class LedgerProjection(ILedgerReadModelStore store) : IProjection
{
    public Task ApplyAsync(StoredEvent storedEvent, CancellationToken cancellationToken)
    {
        if (storedEvent.Event is not BalanceChangedEvent e)
        {
            return Task.CompletedTask;
        }

        (string Kind, Guid? Counterparty) info = e switch
        {
            FundsDepositedEvent => ("deposit", null),
            FundsWithdrawnEvent => ("withdrawal", null),
            TransferInitiatedEvent t => ("transfer_out", t.DestinationAccountId),
            TransferReceivedEvent t => ("transfer_in", t.SourceAccountId),
            _ => ("adjustment", null),
        };

        var entry = new LedgerEntryReadModel(
            storedEvent.GlobalPosition,
            e.TransactionId,
            e.AccountId,
            info.Kind,
            e.EntryType,
            e.Amount,
            e.Currency,
            e.BalanceAfter,
            e.Description,
            info.Counterparty,
            e.OccurredAt);

        return store.AppendAsync(entry, cancellationToken);
    }

    public Task ResetAsync(CancellationToken cancellationToken) => store.ResetAsync(cancellationToken);
}
