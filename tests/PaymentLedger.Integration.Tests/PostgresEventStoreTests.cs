using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PaymentLedger.Domain.Abstractions;
using PaymentLedger.Domain.Accounts;
using PaymentLedger.Domain.Accounts.Events;
using PaymentLedger.Domain.Common;
using PaymentLedger.Infrastructure.Persistence.Postgres;
using PaymentLedger.Infrastructure.Serialization;
using PaymentLedger.Integration.Tests.Infrastructure;

namespace PaymentLedger.Integration.Tests;

[Collection(PostgresCollection.Name)]
public sealed class PostgresEventStoreTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private NpgsqlDataSource? _dataSource;
    private PostgresEventStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        _dataSource = NpgsqlDataSource.Create(await postgres.CreateDatabaseAsync());
        await new DatabaseInitializer(_dataSource, NullLogger<DatabaseInitializer>.Instance).StartAsync(Ct);
        _store = new PostgresEventStore(_dataSource, new EventSerializer(), TimeProvider.System);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }
    }

    [Fact]
    public async Task Events_round_trip_with_their_concrete_types()
    {
        var accountId = Guid.NewGuid();
        var created = new AccountCreatedEvent(accountId, "Alice", "BRL", AccountType.Liability, IsSystemAccount: false);

        await _store.AppendAsync([new StreamAppend(accountId, -1, [created])], Ct);
        var stream = await _store.ReadStreamAsync(accountId, Ct);

        var stored = stream.ShouldHaveSingleItem();
        stored.Version.ShouldBe(0);
        stored.Event.ShouldBeOfType<AccountCreatedEvent>().ShouldBe(created);
    }

    [Fact]
    public async Task Stale_expected_version_is_rejected()
    {
        var accountId = Guid.NewGuid();
        await _store.AppendAsync([new StreamAppend(accountId, -1, [Created(accountId)])], Ct);

        var error = await Should.ThrowAsync<ConcurrencyException>(
            () => _store.AppendAsync([new StreamAppend(accountId, -1, [new AccountFrozenEvent(accountId, "late")])], Ct));

        error.ActualVersion.ShouldBe(0);
        (await _store.ReadStreamAsync(accountId, Ct)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Multi_stream_appends_are_all_or_nothing()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await _store.AppendAsync([new StreamAppend(b, -1, [Created(b)])], Ct);

        // Stream a is fine, stream b is stale: neither may be written.
        await Should.ThrowAsync<ConcurrencyException>(() => _store.AppendAsync(
        [
            new StreamAppend(a, -1, [Created(a)]),
            new StreamAppend(b, -1, [new AccountFrozenEvent(b, "stale")]),
        ], Ct));

        (await _store.ReadStreamAsync(a, Ct)).ShouldBeEmpty();
        (await _store.ReadStreamAsync(b, Ct)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Global_log_is_read_in_commit_order_across_pages()
    {
        var ids = Enumerable.Range(0, 600).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var id in ids)
        {
            await _store.AppendAsync([new StreamAppend(id, -1, [Created(id)])], Ct);
        }

        var positions = new List<long>();
        await foreach (var stored in _store.ReadAllAsync(cancellationToken: Ct))
        {
            positions.Add(stored.GlobalPosition);
        }

        positions.Count.ShouldBe(ids.Length);
        positions.ShouldBeInOrder(SortDirection.Ascending);
    }

    [Fact]
    public async Task Every_event_gets_an_outbox_row_in_the_same_transaction()
    {
        var accountId = Guid.NewGuid();
        await _store.AppendAsync([new StreamAppend(accountId, -1, [Created(accountId), new AccountFrozenEvent(accountId, "kyc")])], Ct);

        var stats = await new PostgresOutboxStore(_dataSource!, TimeProvider.System).GetStatsAsync(10, Ct);

        stats.PendingCount.ShouldBe(2);
    }

    private static IDomainEvent Created(Guid id) =>
        new AccountCreatedEvent(id, "Customer", "BRL", AccountType.Liability, IsSystemAccount: false);
}
