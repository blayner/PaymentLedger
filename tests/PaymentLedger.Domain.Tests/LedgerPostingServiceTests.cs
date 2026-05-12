using PaymentLedger.Domain.Accounts;
using PaymentLedger.Domain.Accounts.Events;
using PaymentLedger.Domain.Common;
using PaymentLedger.Domain.Ledger;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Domain.Tests;

public sealed class LedgerPostingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly LedgerPostingService _posting = new();
    private readonly Account _settlement = Account.OpenSystem(SystemAccounts.SettlementAccountId("BRL"), "Settlement BRL", "BRL");
    private readonly Account _alice = Account.Open(Guid.NewGuid(), "Alice", "BRL");
    private readonly Account _bob = Account.Open(Guid.NewGuid(), "Bob", "BRL");

    private static Money Brl(decimal amount) => new(amount, "BRL");

    [Fact]
    public void Deposit_increases_both_the_liability_and_the_asset()
    {
        var tx = PostingRules.Deposit(Guid.NewGuid(), _alice.Id, _settlement.Id, Brl(100m), "top-up", Now);

        _posting.Post(tx, [_alice, _settlement]);

        _alice.Balance.ShouldBe(Brl(100m));
        _settlement.Balance.ShouldBe(Brl(100m));
        _alice.UncommittedEvents.OfType<FundsDepositedEvent>().ShouldHaveSingleItem().EntryType.ShouldBe(EntryType.Credit);
        _settlement.UncommittedEvents.OfType<FundsDepositedEvent>().ShouldHaveSingleItem().EntryType.ShouldBe(EntryType.Debit);
    }

    [Fact]
    public void Transfer_moves_funds_and_records_counterparties()
    {
        Fund(_alice, 100m);
        var txId = Guid.NewGuid();

        _posting.Post(PostingRules.Transfer(txId, _alice.Id, _bob.Id, Brl(30m), "rent", Now), [_alice, _bob]);

        _alice.Balance.ShouldBe(Brl(70m));
        _bob.Balance.ShouldBe(Brl(30m));
        _alice.UncommittedEvents.OfType<TransferInitiatedEvent>().ShouldHaveSingleItem().DestinationAccountId.ShouldBe(_bob.Id);
        _bob.UncommittedEvents.OfType<TransferReceivedEvent>().ShouldHaveSingleItem().SourceAccountId.ShouldBe(_alice.Id);
        _alice.HasApplied(txId).ShouldBeTrue();
        _bob.HasApplied(txId).ShouldBeTrue();
    }

    [Fact]
    public void Insufficient_funds_leaves_every_account_untouched()
    {
        Fund(_alice, 10m);
        var aliceEvents = _alice.UncommittedEvents.Count;
        var bobEvents = _bob.UncommittedEvents.Count;

        var error = Should.Throw<DomainException>(() => _posting.Post(
            PostingRules.Transfer(Guid.NewGuid(), _alice.Id, _bob.Id, Brl(10.01m), "too much", Now),
            [_alice, _bob]));

        error.Code.ShouldBe("account.insufficient_funds");
        _alice.UncommittedEvents.Count.ShouldBe(aliceEvents);
        _bob.UncommittedEvents.Count.ShouldBe(bobEvents);
        _alice.Balance.ShouldBe(Brl(10m));
        _bob.Balance.ShouldBe(Brl(0m));
    }

    [Fact]
    public void A_failure_on_the_second_entry_does_not_apply_the_first()
    {
        Fund(_alice, 50m);
        _bob.Freeze("compliance hold");

        Should.Throw<DomainException>(() => _posting.Post(
            PostingRules.Transfer(Guid.NewGuid(), _alice.Id, _bob.Id, Brl(20m), "blocked", Now),
            [_alice, _bob]));

        _alice.Balance.ShouldBe(Brl(50m));
    }

    [Fact]
    public void The_same_transaction_cannot_be_posted_twice()
    {
        var tx = PostingRules.Deposit(Guid.NewGuid(), _alice.Id, _settlement.Id, Brl(5m), "once", Now);
        _posting.Post(tx, [_alice, _settlement]);

        var error = Should.Throw<DomainException>(() => _posting.Post(tx, [_alice, _settlement]));

        error.Code.ShouldBe("transaction.duplicate");
        _alice.Balance.ShouldBe(Brl(5m));
    }

    [Fact]
    public void Every_referenced_account_must_be_supplied()
    {
        var tx = PostingRules.Transfer(Guid.NewGuid(), _alice.Id, _bob.Id, Brl(1m), "missing", Now);

        var error = Should.Throw<DomainException>(() => _posting.Post(tx, [_alice]));

        error.Code.ShouldBe("posting.missing_account");
    }

    [Fact]
    public void Currency_of_the_entry_must_match_the_account()
    {
        var usdSettlement = Account.OpenSystem(SystemAccounts.SettlementAccountId("USD"), "Settlement USD", "USD");
        var tx = PostingRules.Deposit(Guid.NewGuid(), _alice.Id, usdSettlement.Id, new Money(1m, "USD"), "fx", Now);

        var error = Should.Throw<DomainException>(() => _posting.Post(tx, [_alice, usdSettlement]));

        error.Code.ShouldBe("posting.currency_mismatch");
    }

    [Fact]
    public void Sum_of_signed_balances_stays_zero_across_many_postings()
    {
        Fund(_alice, 500m);
        Fund(_bob, 200m);
        _posting.Post(PostingRules.Transfer(Guid.NewGuid(), _alice.Id, _bob.Id, Brl(125.5m), "t1", Now), [_alice, _bob]);
        _posting.Post(PostingRules.Withdrawal(Guid.NewGuid(), _bob.Id, _settlement.Id, Brl(80m), "w1", Now), [_bob, _settlement]);

        // Assets = Liabilities: what the platform holds equals what it owes its customers.
        _settlement.Balance.ShouldBe(_alice.Balance + _bob.Balance);
    }

    private void Fund(Account account, decimal amount) =>
        _posting.Post(
            PostingRules.Deposit(Guid.NewGuid(), account.Id, _settlement.Id, Brl(amount), "fund", Now),
            [account, _settlement]);
}
