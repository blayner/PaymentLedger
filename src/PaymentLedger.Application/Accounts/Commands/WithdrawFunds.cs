using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Messaging;
using PaymentLedger.Domain.Ledger;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Application.Accounts.Commands;

public sealed record WithdrawFundsCommand(
    Guid AccountId,
    decimal Amount,
    string Currency,
    Guid TransactionId,
    string? Description) : ICommand<TransactionResult>, IRetryOnConcurrency;

public sealed class WithdrawFundsCommandHandler(
    IAccountRepository accounts,
    LedgerPostingService posting,
    TimeProvider clock) : ICommandHandler<WithdrawFundsCommand, TransactionResult>
{
    public async Task<TransactionResult> Handle(WithdrawFundsCommand request, CancellationToken cancellationToken)
    {
        var customer = await accounts.GetRequiredAsync(request.AccountId, cancellationToken);

        var fingerprint = RequestFingerprints.For(request);

        if (IdempotencyGuard.IsReplay(customer, request.TransactionId, fingerprint))
        {
            return new TransactionResult(request.TransactionId, customer.Id, customer.Balance.Amount, customer.Currency, AlreadyProcessed: true);
        }

        var settlement = await accounts.GetOrOpenSettlementAsync(request.Currency, cancellationToken);
        IdempotencyGuard.EnsureCounterpartIsConsistent(customer, settlement, request.TransactionId, fingerprint);

        var transaction = PostingRules.Withdrawal(
            request.TransactionId,
            customer.Id,
            settlement.Id,
            new Money(request.Amount, request.Currency),
            request.Description ?? "Withdrawal",
            clock.GetUtcNow(),
            fingerprint);

        posting.Post(transaction, [customer, settlement]);
        await accounts.SaveAsync([customer, settlement], cancellationToken);

        return new TransactionResult(request.TransactionId, customer.Id, customer.Balance.Amount, customer.Currency, AlreadyProcessed: false);
    }
}
