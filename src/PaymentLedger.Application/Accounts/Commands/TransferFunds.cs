using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Messaging;
using PaymentLedger.Domain.Common;
using PaymentLedger.Domain.Ledger;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Application.Accounts.Commands;

public sealed record TransferFundsCommand(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency,
    Guid TransactionId,
    string? Description) : ICommand<TransactionResult>, IRetryOnConcurrency;

/// <summary>
/// Moves funds between two customer accounts. Both streams are appended in one atomic
/// write, so there is no intermediate state where money has left the source but not
/// reached the destination. That keeps internal transfers free of a compensating saga.
/// </summary>
public sealed class TransferFundsCommandHandler(
    IAccountRepository accounts,
    LedgerPostingService posting,
    TimeProvider clock) : ICommandHandler<TransferFundsCommand, TransactionResult>
{
    public async Task<TransactionResult> Handle(TransferFundsCommand request, CancellationToken cancellationToken)
    {
        if (request.SourceAccountId == request.DestinationAccountId)
        {
            throw new DomainException("transfer.same_account", "Source and destination accounts must differ.");
        }

        var source = await accounts.GetRequiredAsync(request.SourceAccountId, cancellationToken);

        var fingerprint = RequestFingerprints.For(request);

        if (IdempotencyGuard.IsReplay(source, request.TransactionId, fingerprint))
        {
            return new TransactionResult(request.TransactionId, source.Id, source.Balance.Amount, source.Currency, AlreadyProcessed: true);
        }

        var destination = await accounts.GetRequiredAsync(request.DestinationAccountId, cancellationToken);
        IdempotencyGuard.EnsureCounterpartIsConsistent(source, destination, request.TransactionId, fingerprint);

        if (source.IsSystemAccount || destination.IsSystemAccount)
        {
            throw new DomainException("transfer.system_account", "Transfers are only allowed between customer accounts.");
        }

        var transaction = PostingRules.Transfer(
            request.TransactionId,
            source.Id,
            destination.Id,
            new Money(request.Amount, request.Currency),
            request.Description ?? "Transfer",
            clock.GetUtcNow(),
            fingerprint);

        posting.Post(transaction, [source, destination]);
        await accounts.SaveAsync([source, destination], cancellationToken);

        return new TransactionResult(request.TransactionId, source.Id, source.Balance.Amount, source.Currency, AlreadyProcessed: false);
    }
}
