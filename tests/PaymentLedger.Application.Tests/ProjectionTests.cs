using Microsoft.Extensions.Logging.Abstractions;
using PaymentLedger.Application.Ledger.Queries;
using PaymentLedger.Application.Projections;
using PaymentLedger.Application.Tests.Support;
using PaymentLedger.Domain.Accounts;
using PaymentLedger.Domain.Ledger;

namespace PaymentLedger.Application.Tests;

public sealed class ProjectionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly LedgerHarness _harness = new();

    [Fact]
    public async Task Account_read_model_reflects_committed_events()
    {
        var alice = await _harness.OpenAccountAsync();
        await _harness.DepositAsync(alice, 80m);
        await _harness.WithdrawAsync(alice, 30m);

        var model = (await _harness.AccountReadModels.GetAsync(alice, Ct)).ShouldNotBeNull();

        model.Balance.ShouldBe(50m);
        model.TransactionCount.ShouldBe(2);
        model.Status.ShouldBe(AccountStatus.Active);
    }

    [Fact]
    public async Task Ledger_has_one_debit_and_one_credit_per_transaction()
    {
        var alice = await _harness.OpenAccountAsync("Alice");
        var bob = await _harness.OpenAccountAsync("Bob");
        await _harness.DepositAsync(alice, 100m);
        var transfer = await _harness.TransferAsync(alice, bob, 40m);

        var entries = await _harness.LedgerReadModels.GetByTransactionAsync(transfer.TransactionId, Ct);

        entries.Count.ShouldBe(2);
        entries.ShouldContain(e => e.AccountId == alice && e.EntryType == EntryType.Debit && e.Kind == "transfer_out", 1);
        entries.ShouldContain(e => e.AccountId == bob && e.EntryType == EntryType.Credit && e.Kind == "transfer_in", 1);
    }

    [Fact]
    public async Task Trial_balance_is_balanced_after_mixed_activity()
    {
        var alice = await _harness.OpenAccountAsync("Alice");
        var bob = await _harness.OpenAccountAsync("Bob");
        await _harness.DepositAsync(alice, 300m);
        await _harness.DepositAsync(bob, 50m);
        await _harness.TransferAsync(alice, bob, 120.40m);
        await _harness.WithdrawAsync(bob, 70m);

        var handler = new GetTrialBalanceQueryHandler(_harness.LedgerReadModels, _harness.Clock);
        var trialBalance = await handler.Handle(new GetTrialBalanceQuery(), Ct);

        trialBalance.IsBalanced.ShouldBeTrue();
        var line = trialBalance.Lines.ShouldHaveSingleItem();
        line.Currency.ShouldBe("BRL");
        line.TotalDebits.ShouldBe(540.40m);
        line.TotalCredits.ShouldBe(540.40m);
    }

    [Fact]
    public async Task Read_models_can_be_rebuilt_from_the_event_log()
    {
        var alice = await _harness.OpenAccountAsync();
        await _harness.DepositAsync(alice, 25m);

        await _harness.AccountReadModels.ResetAsync(Ct);
        await _harness.LedgerReadModels.ResetAsync(Ct);

        var rebuilt = new ProjectionRunner(
            _harness.EventStore,
            [new AccountProjection(_harness.AccountReadModels), new LedgerProjection(_harness.LedgerReadModels)],
            NullLogger<ProjectionRunner>.Instance);
        await rebuilt.CatchUpAsync(Ct);

        (await _harness.AccountReadModels.GetAsync(alice, Ct)).ShouldNotBeNull().Balance.ShouldBe(25m);
        (await _harness.LedgerReadModels.GetByAccountAsync(alice, 1, 10, Ct)).TotalCount.ShouldBe(1);
    }
}
