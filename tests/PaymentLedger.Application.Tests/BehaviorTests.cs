using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentLedger.Application.Accounts.Commands;
using PaymentLedger.Application.Behaviors;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Messaging;
using PaymentLedger.Application.Validation;
using PaymentLedger.Domain.Abstractions;

namespace PaymentLedger.Application.Tests;

public sealed class BehaviorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TransactionResult Ok = new(Guid.NewGuid(), Guid.NewGuid(), 10m, "BRL", false);

    private static DepositFundsCommand ValidDeposit() =>
        new(Guid.NewGuid(), 10m, "BRL", Guid.NewGuid(), "test");

    private static ConcurrencyRetryBehavior<TRequest, TResponse> RetryBehavior<TRequest, TResponse>()
        where TRequest : notnull =>
        new(NullLogger<ConcurrencyRetryBehavior<TRequest, TResponse>>.Instance);

    [Fact]
    public async Task Retries_retryable_commands_until_the_conflict_clears()
    {
        var calls = 0;
        RequestHandlerDelegate<TransactionResult> next = () =>
            ++calls < 3
                ? throw new ConcurrencyException(Guid.NewGuid(), 1, 2)
                : Task.FromResult(Ok);

        var result = await RetryBehavior<DepositFundsCommand, TransactionResult>().Handle(ValidDeposit(), next, Ct);

        result.ShouldBe(Ok);
        calls.ShouldBe(3);
    }

    [Fact]
    public async Task Gives_up_after_max_attempts()
    {
        var calls = 0;
        RequestHandlerDelegate<TransactionResult> next = () =>
        {
            calls++;
            throw new ConcurrencyException(Guid.NewGuid(), 1, 2);
        };

        await Should.ThrowAsync<ConcurrencyException>(
            () => RetryBehavior<DepositFundsCommand, TransactionResult>().Handle(ValidDeposit(), next, Ct));

        calls.ShouldBe(ConcurrencyRetryBehavior<DepositFundsCommand, TransactionResult>.MaxAttempts);
    }

    [Fact]
    public async Task Does_not_retry_commands_that_are_not_marked_retryable()
    {
        var calls = 0;
        RequestHandlerDelegate<Unit> next = () =>
        {
            calls++;
            throw new ConcurrencyException(Guid.NewGuid(), 1, 2);
        };

        await Should.ThrowAsync<ConcurrencyException>(
            () => RetryBehavior<CloseAccountCommand, Unit>().Handle(new CloseAccountCommand(Guid.NewGuid()), next, Ct));

        calls.ShouldBe(1);
    }

    [Fact]
    public async Task Validation_short_circuits_before_the_handler()
    {
        var behavior = new ValidationBehavior<DepositFundsCommand, TransactionResult>([new DepositFundsCommandValidator()]);
        var handlerCalled = false;
        RequestHandlerDelegate<TransactionResult> next = () =>
        {
            handlerCalled = true;
            return Task.FromResult(Ok);
        };
        var invalid = ValidDeposit() with { Amount = 10.005m, Currency = "brl" };

        var error = await Should.ThrowAsync<ValidationException>(() => behavior.Handle(invalid, next, Ct));

        error.Errors.Select(e => e.PropertyName).OrderBy(p => p, StringComparer.Ordinal).ToArray()
            .ShouldBe(new[] { "Amount", "Currency" });
        handlerCalled.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Transfer_validator_rejects_non_positive_amounts(int amount)
    {
        var validator = new TransferFundsCommandValidator();

        var result = validator.Validate(new TransferFundsCommand(Guid.NewGuid(), Guid.NewGuid(), amount, "BRL", Guid.NewGuid(), null));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(TransferFundsCommand.Amount));
    }
}
