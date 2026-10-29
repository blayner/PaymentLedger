using PaymentLedger.Application.Accounts.Commands;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Tests.Support;
using PaymentLedger.Domain.Abstractions;
using PaymentLedger.Domain.Accounts.Events;
using PaymentLedger.Infrastructure.Serialization;

namespace PaymentLedger.Application.Tests;

public sealed class IdempotencyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly LedgerHarness _harness = new();

    [Fact]
    public async Task Replay_with_an_equivalent_payload_returns_the_original_result()
    {
        var alice = await _harness.OpenAccountAsync();
        var key = Guid.NewGuid();
        await _harness.DepositAsync(alice, 100.50m, key, description: "salary");

        // Same movement: trailing zeros, lower-case currency and a new description don't matter.
        var replay = await _harness.DepositAsync(alice, 100.5000m, key, currency: "BRL", description: "salary (retry)");

        replay.AlreadyProcessed.ShouldBeTrue();
        replay.Balance.ShouldBe(100.50m);
        (await _harness.EventStore.ReadStreamAsync(alice, Ct)).Count.ShouldBe(2, "created + one deposit");
    }

    [Fact]
    public async Task Reusing_a_deposit_key_with_another_amount_is_rejected_and_persists_nothing()
    {
        var alice = await _harness.OpenAccountAsync();
        var key = Guid.NewGuid();
        await _harness.DepositAsync(alice, 100m, key);

        var error = await Should.ThrowAsync<IdempotencyKeyReusedException>(() => _harness.DepositAsync(alice, 101m, key));

        error.IdempotencyKey.ShouldBe(key);
        (await _harness.EventStore.ReadStreamAsync(alice, Ct)).Count.ShouldBe(2);
        (await _harness.Accounts.GetAsync(alice, Ct)).ShouldNotBeNull().Balance.Amount.ShouldBe(100m);
    }

    [Fact]
    public async Task Reusing_a_deposit_key_for_a_withdrawal_is_rejected()
    {
        var alice = await _harness.OpenAccountAsync();
        var key = Guid.NewGuid();
        await _harness.DepositAsync(alice, 100m, key);

        await Should.ThrowAsync<IdempotencyKeyReusedException>(() => _harness.WithdrawAsync(alice, 100m, key));
    }

    [Fact]
    public async Task Reusing_a_transfer_key_with_another_destination_is_rejected()
    {
        var alice = await _harness.OpenAccountAsync("Alice");
        var bob = await _harness.OpenAccountAsync("Bob");
        var carol = await _harness.OpenAccountAsync("Carol");
        await _harness.DepositAsync(alice, 100m);
        var key = Guid.NewGuid();
        await _harness.TransferAsync(alice, bob, 10m, key);

        await Should.ThrowAsync<IdempotencyKeyReusedException>(() => _harness.TransferAsync(alice, carol, 10m, key));

        (await _harness.TransferAsync(alice, bob, 10m, key)).AlreadyProcessed.ShouldBeTrue();
        (await _harness.Accounts.GetAsync(carol, Ct)).ShouldNotBeNull().Balance.Amount.ShouldBe(0m);
    }

    [Fact]
    public async Task Reusing_a_key_on_another_customer_is_caught_through_the_shared_settlement_account()
    {
        var alice = await _harness.OpenAccountAsync("Alice");
        var bob = await _harness.OpenAccountAsync("Bob");
        var key = Guid.NewGuid();
        await _harness.DepositAsync(bob, 10m, key);

        // Alice has never seen the key, but the BRL settlement account has, with a different fingerprint.
        await Should.ThrowAsync<IdempotencyKeyReusedException>(() => _harness.DepositAsync(alice, 10m, key));

        (await _harness.Accounts.GetAsync(alice, Ct)).ShouldNotBeNull().Balance.Amount.ShouldBe(0m);
    }

    [Fact]
    public async Task A_stale_read_of_the_primary_account_is_surfaced_as_a_concurrency_conflict()
    {
        var alice = await _harness.OpenAccountAsync();
        var key = Guid.NewGuid();
        var staleAlice = await _harness.Accounts.GetAsync(alice, Ct);
        await _harness.DepositAsync(alice, 10m, key);
        var settlement = await _harness.Accounts.GetAsync(Domain.Accounts.SystemAccounts.SettlementAccountId("BRL"), Ct);
        var fingerprint = RequestFingerprints.For(new DepositFundsCommand(alice, 10m, "BRL", key, null));

        Should.Throw<ConcurrencyException>(
            () => IdempotencyGuard.EnsureCounterpartIsConsistent(staleAlice!, settlement!, key, fingerprint));
    }

    [Fact]
    public async Task Different_keys_with_the_same_payload_are_separate_movements()
    {
        var alice = await _harness.OpenAccountAsync();

        await _harness.DepositAsync(alice, 25m, Guid.NewGuid());
        var second = await _harness.DepositAsync(alice, 25m, Guid.NewGuid());

        second.AlreadyProcessed.ShouldBeFalse();
        second.Balance.ShouldBe(50m);
    }

    [Fact]
    public async Task The_fingerprint_is_persisted_on_both_sides_of_the_posting()
    {
        var alice = await _harness.OpenAccountAsync();
        var key = Guid.NewGuid();
        await _harness.DepositAsync(alice, 10m, key);

        var expected = RequestFingerprints.For(new DepositFundsCommand(alice, 10m, "BRL", key, null));
        var postings = new List<BalanceChangedEvent>();
        await foreach (var stored in _harness.EventStore.ReadAllAsync(0, Ct))
        {
            if (stored.Event is BalanceChangedEvent posting && posting.TransactionId == key)
            {
                postings.Add(posting);
            }
        }

        postings.Count.ShouldBe(2, "customer and settlement");

        postings.ShouldAllBe(e => e.RequestFingerprint == expected);
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_apply_once()
    {
        var alice = await _harness.OpenAccountAsync();
        var key = Guid.NewGuid();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => DepositWithRetryAsync(alice, 40m, key), Ct)));

        results.Count(r => !r.AlreadyProcessed).ShouldBe(1);
        (await _harness.Accounts.GetAsync(alice, Ct)).ShouldNotBeNull().Balance.Amount.ShouldBe(40m);
    }

    [Fact]
    public void Fingerprints_are_canonical_and_versioned()
    {
        var account = Guid.NewGuid();
        var key = Guid.NewGuid();

        var a = RequestFingerprints.For(new DepositFundsCommand(account, 10.50m, "brl", key, "a"));
        var b = RequestFingerprints.For(new DepositFundsCommand(account, 10.5m, " BRL ", Guid.NewGuid(), "b"));
        var withdrawal = RequestFingerprints.For(new WithdrawFundsCommand(account, 10.5m, "BRL", key, null));

        a.ShouldBe(b, "the key, description and formatting are not part of the fingerprint");
        a.ShouldNotBe(withdrawal, "the operation is");
        a.Length.ShouldBe(64);
        a.ShouldBe(a.ToLowerInvariant());
    }

    [Fact]
    public void Events_written_before_fingerprints_existed_still_deserialize()
    {
        var serializer = new EventSerializer();
        const string legacyJson = """
            {"accountId":"8d2f3c1e-0000-0000-0000-000000000001","transactionId":"8d2f3c1e-0000-0000-0000-000000000002",
             "entryType":"Credit","amount":10,"currency":"BRL","balanceAfter":10,"description":"deposit"}
            """;

        var @event = serializer.Deserialize(nameof(FundsDepositedEvent), legacyJson).ShouldBeOfType<FundsDepositedEvent>();

        @event.RequestFingerprint.ShouldBeNull();
        serializer.Serialize(@event with { RequestFingerprint = "abc" }).ShouldContain("\"requestFingerprint\":\"abc\"");
    }

    // The harness calls handlers directly, so it stands in for ConcurrencyRetryBehavior here.
    private async Task<TransactionResult> DepositWithRetryAsync(Guid account, decimal amount, Guid key)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await _harness.DepositAsync(account, amount, key);
            }
            catch (ConcurrencyException) when (attempt < 10)
            {
                await Task.Yield();
            }
        }
    }
}
