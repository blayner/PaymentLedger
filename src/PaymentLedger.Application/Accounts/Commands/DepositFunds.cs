using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Messaging;
using PaymentLedger.Domain.Ledger;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Application.Accounts.Commands;

/// <param name="TransactionId">Client-supplied idempotency key. Retrying with the same id is safe.</param>
public sealed record DepositFundsCommand(
    Guid AccountId,
    decimal Amount,
    string Currency,
    Guid TransactionId,
    string? Description) : ICommand<TransactionResult>, IRetryOnConcurrency;

public sealed class DepositFundsCommandHandler(
    IAccountRepository accounts,
    LedgerPostingService posting,
    TimeProvider clock) : ICommandHandler<DepositFundsCommand, TransactionResult>
{
    public async Task<TransactionResult> Handle(DepositFundsCommand request, CancellationToken cancellationToken)
    {
        var customer = await accounts.GetRequiredAsync(request.AccountId, cancellationToken);

        var fingerprint = RequestFingerprints.For(request);

        if (IdempotencyGuard.IsReplay(customer, request.TransactionId, fingerprint))
        {
            return new TransactionResult(request.TransactionId, customer.Id, customer.Balance.Amount, customer.Currency, AlreadyProcessed: true);
        }

        var settlement = await accounts.GetOrOpenSettlementAsync(request.Currency, cancellationToken);
        IdempotencyGuard.EnsureCounterpartIsConsistent(customer, settlement, request.TransactionId, fingerprint);

        var transaction = PostingRules.Deposit(
            request.TransactionId,
            customer.Id,
            settlement.Id,
            new Money(request.Amount, request.Currency),
            request.Description ?? "Deposit",
            clock.GetUtcNow(),
            fingerprint);

        posting.Post(transaction, [customer, settlement]);
        await accounts.SaveAsync([customer, settlement], cancellationToken);

        return new TransactionResult(request.TransactionId, customer.Id, customer.Balance.Amount, customer.Currency, AlreadyProcessed: false);
    }
}
