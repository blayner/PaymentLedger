using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Messaging;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Application.Accounts.Commands;

public sealed record WithdrawFundsCommand(
    Guid AccountId,
    decimal Amount,
    string Currency,
    Guid TransactionId,
    string? Description) : ICommand<TransactionResult>;

public sealed class WithdrawFundsCommandHandler(IAccountRepository accounts)
    : ICommandHandler<WithdrawFundsCommand, TransactionResult>
{
    public async Task<TransactionResult> Handle(WithdrawFundsCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.GetRequiredAsync(request.AccountId, cancellationToken);

        if (account.HasApplied(request.TransactionId))
        {
            return new TransactionResult(request.TransactionId, account.Id, account.Balance.Amount, account.Currency, AlreadyProcessed: true);
        }

        account.Withdraw(new Money(request.Amount, request.Currency), request.TransactionId, request.Description ?? "Withdrawal");
        await accounts.SaveAsync([account], cancellationToken);

        return new TransactionResult(request.TransactionId, account.Id, account.Balance.Amount, account.Currency, AlreadyProcessed: false);
    }
}
