using PaymentLedger.Application.Common;
using PaymentLedger.Application.Tests.Support;
using PaymentLedger.Domain.Accounts;
using PaymentLedger.Domain.Common;

namespace PaymentLedger.Application.Tests;

public sealed class MoneyMovementHandlerTests
{
    private readonly LedgerHarness _harness = new();

    [Fact]
    public async Task Deposit_credits_the_customer_and_debits_settlement_atomically()
    {
        var alice = await _harness.OpenAccountAsync();

        var result = await _harness.DepositAsync(alice, 150m);

        result.Balance.ShouldBe(150m);
        result.AlreadyProcessed.ShouldBeFalse();

        var settlement = await _harness.Accounts.GetAsync(SystemAccounts.SettlementAccountId("BRL"), default);
        settlement.ShouldNotBeNull().Balance.Amount.ShouldBe(150m);
    }

    [Fact]
    public async Task Replaying_a_deposit_with_the_same_transaction_id_is_a_no_op()
    {
        var alice = await _harness.OpenAccountAsync();
        var transactionId = Guid.NewGuid();

        await _harness.DepositAsync(alice, 100m, transactionId);
        var replay = await _harness.DepositAsync(alice, 100m, transactionId);

        replay.AlreadyProcessed.ShouldBeTrue();
        replay.Balance.ShouldBe(100m);
        (await _harness.EventStore.ReadStreamAsync(alice)).Count.ShouldBe(2, "created + one deposit");
    }

    [Fact]
    public async Task Withdrawal_beyond_balance_is_rejected_and_nothing_is_persisted()
    {
        var alice = await _harness.OpenAccountAsync();
        await _harness.DepositAsync(alice, 40m);
        var settlementId = SystemAccounts.SettlementAccountId("BRL");
        var settlementEventsBefore = (await _harness.EventStore.ReadStreamAsync(settlementId)).Count;

        var error = await Should.ThrowAsync<DomainException>(() => _harness.WithdrawAsync(alice, 40.01m));

        error.Code.ShouldBe("account.insufficient_funds");
        (await _harness.EventStore.ReadStreamAsync(settlementId)).Count.ShouldBe(settlementEventsBefore);
    }

    [Fact]
    public async Task Deposit_in_a_foreign_currency_does_not_create_a_settlement_account()
    {
        var alice = await _harness.OpenAccountAsync(currency: "BRL");

        var error = await Should.ThrowAsync<DomainException>(() => _harness.DepositAsync(alice, 10m, currency: "USD"));

        error.Code.ShouldBe("posting.currency_mismatch");
        (await _harness.Accounts.GetAsync(SystemAccounts.SettlementAccountId("USD"), default)).ShouldBeNull();
    }

    [Fact]
    public async Task Unknown_accounts_raise_not_found()
    {
        await Should.ThrowAsync<NotFoundException>(() => _harness.DepositAsync(Guid.NewGuid(), 10m));
    }

    [Fact]
    public async Task Transfer_moves_funds_between_customers()
    {
        var alice = await _harness.OpenAccountAsync("Alice");
        var bob = await _harness.OpenAccountAsync("Bob");
        await _harness.DepositAsync(alice, 100m);

        var result = await _harness.TransferAsync(alice, bob, 35.25m);

        result.Balance.ShouldBe(64.75m);
        (await _harness.Accounts.GetAsync(bob, default)).ShouldNotBeNull().Balance.Amount.ShouldBe(35.25m);
    }

    [Fact]
    public async Task Transfer_to_the_same_account_is_rejected()
    {
        var alice = await _harness.OpenAccountAsync();

        var error = await Should.ThrowAsync<DomainException>(() => _harness.TransferAsync(alice, alice, 1m));

        error.Code.ShouldBe("transfer.same_account");
    }

    [Fact]
    public async Task Transfers_involving_system_accounts_are_rejected()
    {
        var alice = await _harness.OpenAccountAsync();
        await _harness.DepositAsync(alice, 10m);

        var error = await Should.ThrowAsync<DomainException>(
            () => _harness.TransferAsync(alice, SystemAccounts.SettlementAccountId("BRL"), 1m));

        error.Code.ShouldBe("transfer.system_account");
    }

    [Fact]
    public async Task Every_appended_event_produces_an_outbox_message()
    {
        var alice = await _harness.OpenAccountAsync();
        await _harness.DepositAsync(alice, 10m);

        var stats = await _harness.Outbox.GetStatsAsync(maxAttempts: 10, default);

        // created(alice) + created(settlement) + deposit on each side
        stats.PendingCount.ShouldBe(4);
    }
}
