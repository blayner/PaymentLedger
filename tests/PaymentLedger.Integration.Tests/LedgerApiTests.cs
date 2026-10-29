using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Integration.Tests.Infrastructure;

namespace PaymentLedger.Integration.Tests;

[Collection(PostgresCollection.Name)]
public sealed class LedgerApiTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private PaymentLedgerApiFactory? _factory;
    private LedgerApiClient _api = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new PaymentLedgerApiFactory(await postgres.CreateDatabaseAsync());
        _api = new LedgerApiClient(_factory.CreateClient());
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Opening_an_account_returns_201_with_location()
    {
        var response = await _api.Http.PostAsJsonAsync("/api/accounts", new { ownerName = "Alice", currency = "BRL" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        var account = (await response.Content.ReadFromJsonAsync<AccountDto>(Ct)).ShouldNotBeNull();
        account.Balance.ShouldBe(0m);
        account.Status.ShouldBe("Active");
    }

    [Fact]
    public async Task Money_movements_require_an_idempotency_key()
    {
        var alice = await _api.OpenAccountAsync("Alice");

        var response = await _api.DepositAsync(alice.Id, 10m, idempotencyKey: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Retrying_a_deposit_with_the_same_key_posts_it_once()
    {
        var alice = await _api.OpenAccountAsync("Alice");
        var key = Guid.NewGuid();

        var first = await _api.DepositAsync(alice.Id, 100m, key);
        var retry = await _api.DepositAsync(alice.Id, 100m, key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await retry.Content.ReadFromJsonAsync<TransactionResult>(Ct)).ShouldNotBeNull().AlreadyProcessed.ShouldBeTrue();
        (await _api.GetBalanceAsync(alice.Id)).Balance.ShouldBe(100m);
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_payload_returns_422_and_moves_no_money()
    {
        var alice = await _api.OpenAccountAsync("Alice");
        var key = Guid.NewGuid();
        (await _api.DepositAsync(alice.Id, 100m, key)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var reused = await _api.DepositAsync(alice.Id, 250m, key);

        reused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        reused.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await reused.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("type").GetString().ShouldBe("urn:paymentledger:problem:idempotency-key-reused");
        problem.GetProperty("code").GetString().ShouldBe("idempotency.key_reused");
        (await _api.GetBalanceAsync(alice.Id)).Balance.ShouldBe(100m);
    }

    [Fact]
    public async Task Fingerprints_survive_a_restart_because_they_live_in_the_event_store()
    {
        var alice = await _api.OpenAccountAsync("Alice");
        var key = Guid.NewGuid();
        (await _api.DepositAsync(alice.Id, 30m, key)).EnsureSuccessStatusCode();

        // A new host rebuilds every aggregate from PostgreSQL; nothing is cached in memory.
        await using var restarted = new PaymentLedgerApiFactory(_factory!.ConnectionString);
        var api = new LedgerApiClient(restarted.CreateClient());

        (await api.DepositAsync(alice.Id, 30.00m, key)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await api.DepositAsync(alice.Id, 31m, key)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await api.WithdrawAsync(alice.Id, 30m, key)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_apply_exactly_once()
    {
        var alice = await _api.OpenAccountAsync("Alice");
        var key = Guid.NewGuid();
        var statuses = new ConcurrentBag<HttpStatusCode>();

        // All requests load the account before any of them commits. The serialised append with an
        // expected version lets only one write win; the others get a concurrency conflict, are
        // retried, reload the account and find the transaction already applied.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 8),
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (_, _) => statuses.Add((await _api.DepositAsync(alice.Id, 50m, key)).StatusCode));

        statuses.Count(s => s == HttpStatusCode.Created).ShouldBe(1);
        statuses.Count(s => s == HttpStatusCode.OK).ShouldBe(7);
        (await _api.GetBalanceAsync(alice.Id)).Balance.ShouldBe(50m);
    }

    [Fact]
    public async Task Overdraft_is_rejected_with_a_stable_error_code()
    {
        var alice = await _api.OpenAccountAsync("Alice");
        await _api.DepositAsync(alice.Id, 20m, Guid.NewGuid());

        var response = await _api.WithdrawAsync(alice.Id, 20.01m, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("account.insufficient_funds");
    }

    [Fact]
    public async Task Invalid_requests_return_validation_problems()
    {
        var alice = await _api.OpenAccountAsync("Alice");

        var response = await _api.DepositAsync(alice.Id, 10.001m, Guid.NewGuid(), currency: "brl");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").TryGetProperty("Amount", out _).ShouldBeTrue();
        problem.GetProperty("errors").TryGetProperty("Currency", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Ledger_stays_balanced_after_a_mix_of_operations()
    {
        var alice = await _api.OpenAccountAsync("Alice");
        var bob = await _api.OpenAccountAsync("Bob");

        (await _api.DepositAsync(alice.Id, 500m, Guid.NewGuid())).EnsureSuccessStatusCode();
        (await _api.TransferAsync(alice.Id, bob.Id, 175.25m, Guid.NewGuid())).EnsureSuccessStatusCode();
        (await _api.WithdrawAsync(bob.Id, 75.25m, Guid.NewGuid())).EnsureSuccessStatusCode();

        (await _api.GetBalanceAsync(alice.Id)).Balance.ShouldBe(324.75m);
        (await _api.GetBalanceAsync(bob.Id)).Balance.ShouldBe(100m);

        var trialBalance = await _api.GetTrialBalanceAsync();
        trialBalance.GetProperty("isBalanced").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Concurrent_deposits_on_a_hot_settlement_stream_are_all_applied()
    {
        var accounts = await Task.WhenAll(Enumerable.Range(0, 5).Select(i => _api.OpenAccountAsync($"Customer {i}")));
        var statuses = new ConcurrentBag<HttpStatusCode>();

        // 20 deposits across 5 accounts, 4 in flight at a time, all contending for the same
        // BRL settlement stream. Conflicts are absorbed by the retry behaviour.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, 20),
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            async (i, _) =>
            {
                var response = await _api.DepositAsync(accounts[i % accounts.Length].Id, 10m, Guid.NewGuid());
                statuses.Add(response.StatusCode);
            });

        statuses.ShouldAllBe(status => status == HttpStatusCode.Created);

        foreach (var account in accounts)
        {
            (await _api.GetBalanceAsync(account.Id)).Balance.ShouldBe(40m);
        }

        var settlement = (await _api.GetTrialBalanceAsync()).GetProperty("lines")[0];
        settlement.GetProperty("totalDebits").GetDecimal().ShouldBe(200m);
    }

    [Fact]
    public async Task Read_models_are_rebuilt_from_the_event_store_after_a_restart()
    {
        var alice = await _api.OpenAccountAsync("Alice");
        await _api.DepositAsync(alice.Id, 42m, Guid.NewGuid());

        // A brand-new host on the same database starts with empty read models.
        await using var restarted = new PaymentLedgerApiFactory(_factory!.ConnectionString);
        var client = restarted.CreateClient();

        var account = await PollAsync(() => client.GetFromJsonAsync<AccountDto>($"/api/accounts/{alice.Id}", Ct));

        account.Balance.ShouldBe(42m);
    }

    [Fact]
    public async Task Readiness_reports_postgres_and_outbox()
    {
        var response = await _api.Http.GetAsync("/health/ready", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var checks = report.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToArray();
        checks.ShouldBe(new[] { "outbox", "postgres" }, ignoreOrder: true);
    }

    [Fact]
    public async Task Responses_echo_the_correlation_id()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", "test-correlation-123");

        var response = await _api.Http.SendAsync(request, Ct);

        response.Headers.GetValues("X-Correlation-Id").ShouldHaveSingleItem().ShouldBe("test-correlation-123");
    }

    private static async Task<T> PollAsync<T>(Func<Task<T?>> read)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var value = await read();
                if (value is not null)
                {
                    return value;
                }
            }
            catch (HttpRequestException) when (attempt < 50)
            {
                // 404 until the projection catches up
            }

            await Task.Delay(100);
        }
    }
}
