using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Messaging;
using PaymentLedger.Domain.Common;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Application.Accounts.Commands;

public sealed record TransferFundsCommand(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal Amount,
    string Currency,
    Guid TransactionId,
    string? Description) : ICommand<TransactionResult>;

/// <summary>
/// Moves funds between two accounts. Both streams are appended in one atomic write, so
/// there is no intermediate state where money has left the source but not reached the
/// destination. That keeps the design free of a compensating saga for internal transfers.
/// </summary>
public sealed class TransferFundsCommandHandler(IAccountRepository accounts)
    : ICommandHandler<TransferFundsCommand, TransactionResult>
{
    public async Task<TransactionResult> Handle(TransferFundsCommand request, CancellationToken cancellationToken)
    {
        if (request.SourceAccountId == request.DestinationAccountId)
        {
            throw new DomainException("transfer.same_account", "Source and destination accounts must differ.");
        }

        var source = await accounts.GetRequiredAsync(request.SourceAccountId, cancellationToken);
        var destination = await accounts.GetRequiredAsync(request.DestinationAccountId, cancellationToken);

        if (source.HasApplied(request.TransactionId))
        {
            return new TransactionResult(request.TransactionId, source.Id, source.Balance.Amount, source.Currency, AlreadyProcessed: true);
        }

        var amount = new Money(request.Amount, request.Currency);
        var description = request.Description ?? "Transfer";

        source.TransferOut(amount, request.TransactionId, destination.Id, description);
        destination.ReceiveTransfer(amount, request.TransactionId, source.Id, description);

        await accounts.SaveAsync([source, destination], cancellationToken);

        return new TransactionResult(request.TransactionId, source.Id, source.Balance.Amount, source.Currency, AlreadyProcessed: false);
    }
}
