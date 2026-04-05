using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Messaging;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Application.Accounts.Commands;

/// <param name="TransactionId">Client-supplied idempotency key. Retrying with the same id is safe.</param>
public sealed record DepositFundsCommand(
    Guid AccountId,
    decimal Amount,
    string Currency,
    Guid TransactionId,
    string? Description) : ICommand<TransactionResult>;

public sealed class DepositFundsCommandHandler(IAccountRepository accounts)
    : ICommandHandler<DepositFundsCommand, TransactionResult>
{
    public async Task<TransactionResult> Handle(DepositFundsCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.GetRequiredAsync(request.AccountId, cancellationToken);

        if (account.HasApplied(request.TransactionId))
        {
            return new TransactionResult(request.TransactionId, account.Id, account.Balance.Amount, account.Currency, AlreadyProcessed: true);
        }

        account.Deposit(new Money(request.Amount, request.Currency), request.TransactionId, request.Description ?? "Deposit");
        await accounts.SaveAsync([account], cancellationToken);

        return new TransactionResult(request.TransactionId, account.Id, account.Balance.Amount, account.Currency, AlreadyProcessed: false);
    }
}
