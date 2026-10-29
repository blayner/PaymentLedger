using PaymentLedger.Domain.Accounts;
using PaymentLedger.Domain.Accounts.Events;
using PaymentLedger.Domain.Ledger;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Domain.Tests;

public sealed class IdempotencyCheckTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private const string Fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherFingerprint = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private readonly LedgerPostingService _posting = new();

    [Fact]
    public void Unknown_transactions_are_not_applied()
    {
        var (customer, _) = OpenPair();

        customer.CheckIdempotency(Guid.NewGuid(), Fingerprint).ShouldBe(IdempotencyCheck.NotApplied);
    }

    [Fact]
    public void Same_key_and_fingerprint_is_a_replay()
    {
        var (customer, settlement) = OpenPair();
        var key = Guid.NewGuid();
        Deposit(customer, settlement, key, Fingerprint);

        customer.CheckIdempotency(key, Fingerprint).ShouldBe(IdempotencyCheck.Replay);
        settlement.CheckIdempotency(key, Fingerprint).ShouldBe(IdempotencyCheck.Replay);
    }

    [Fact]
    public void Same_key_with_another_fingerprint_is_a_mismatch()
    {
        var (customer, settlement) = OpenPair();
        var key = Guid.NewGuid();
        Deposit(customer, settlement, key, Fingerprint);

        customer.CheckIdempotency(key, OtherFingerprint).ShouldBe(IdempotencyCheck.PayloadMismatch);
    }

    [Fact]
    public void Fingerprints_are_recorded_on_the_events_and_survive_rehydration()
    {
        var (customer, settlement) = OpenPair();
        var key = Guid.NewGuid();
        Deposit(customer, settlement, key, Fingerprint);

        customer.UncommittedEvents.OfType<BalanceChangedEvent>().ShouldHaveSingleItem().RequestFingerprint.ShouldBe(Fingerprint);

        var replayed = Account.Rehydrate(customer.UncommittedEvents);
        replayed.CheckIdempotency(key, OtherFingerprint).ShouldBe(IdempotencyCheck.PayloadMismatch);
    }

    [Fact]
    public void Legacy_events_without_a_fingerprint_are_treated_as_replays()
    {
        var (customer, settlement) = OpenPair();
        var key = Guid.NewGuid();
        Deposit(customer, settlement, key, fingerprint: null);

        customer.CheckIdempotency(key, Fingerprint).ShouldBe(IdempotencyCheck.Replay);
    }

    private static (Account Customer, Account Settlement) OpenPair() =>
        (Account.Open(Guid.NewGuid(), "Alice", "BRL"),
         Account.OpenSystem(SystemAccounts.SettlementAccountId("BRL"), "Settlement BRL", "BRL"));

    private void Deposit(Account customer, Account settlement, Guid key, string? fingerprint) =>
        _posting.Post(
            PostingRules.Deposit(key, customer.Id, settlement.Id, new Money(10m, "BRL"), "deposit", Now, fingerprint),
            [customer, settlement]);
}
