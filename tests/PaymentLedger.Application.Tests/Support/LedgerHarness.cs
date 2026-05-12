using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PaymentLedger.Application.Accounts.Commands;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Projections;
using PaymentLedger.Domain.Ledger;
using PaymentLedger.Infrastructure.EventStore;
using PaymentLedger.Infrastructure.Outbox;
using PaymentLedger.Infrastructure.Persistence;
using PaymentLedger.Infrastructure.ReadModels;
using PaymentLedger.Infrastructure.Serialization;

namespace PaymentLedger.Application.Tests.Support;

/// <summary>
/// Wires handlers to the in-memory event store and read models without the dispatcher
/// pipeline, so each test exercises a handler against real persistence semantics
/// (optimistic concurrency, atomic multi-stream appends, projections).
/// </summary>
internal sealed class LedgerHarness
{
    public LedgerHarness()
    {
        Outbox = new InMemoryOutboxStore(Clock);
        EventStore = new InMemoryEventStore(Clock, Outbox, new EventSerializer());
        Projections = new ProjectionRunner(
            EventStore,
            [new AccountProjection(AccountReadModels), new LedgerProjection(LedgerReadModels)],
            NullLogger<ProjectionRunner>.Instance);
        Accounts = new EventSourcedAccountRepository(EventStore, Projections, NullLogger<EventSourcedAccountRepository>.Instance);
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));

    public InMemoryOutboxStore Outbox { get; }

    public InMemoryEventStore EventStore { get; }

    public InMemoryAccountReadModelStore AccountReadModels { get; } = new();

    public InMemoryLedgerReadModelStore LedgerReadModels { get; } = new();

    public ProjectionRunner Projections { get; }

    public EventSourcedAccountRepository Accounts { get; }

    public LedgerPostingService Posting { get; } = new();

    public Task<Guid> OpenAccountAsync(string owner = "Alice", string currency = "BRL") =>
        new CreateAccountCommandHandler(Accounts).Handle(new CreateAccountCommand(owner, currency), CancellationToken.None);

    public Task<TransactionResult> DepositAsync(Guid accountId, decimal amount, Guid? transactionId = null, string currency = "BRL") =>
        new DepositFundsCommandHandler(Accounts, Posting, Clock).Handle(
            new DepositFundsCommand(accountId, amount, currency, transactionId ?? Guid.NewGuid(), null),
            CancellationToken.None);

    public Task<TransactionResult> WithdrawAsync(Guid accountId, decimal amount, Guid? transactionId = null, string currency = "BRL") =>
        new WithdrawFundsCommandHandler(Accounts, Posting, Clock).Handle(
            new WithdrawFundsCommand(accountId, amount, currency, transactionId ?? Guid.NewGuid(), null),
            CancellationToken.None);

    public Task<TransactionResult> TransferAsync(Guid from, Guid to, decimal amount, Guid? transactionId = null) =>
        new TransferFundsCommandHandler(Accounts, Posting, Clock).Handle(
            new TransferFundsCommand(from, to, amount, "BRL", transactionId ?? Guid.NewGuid(), null),
            CancellationToken.None);
}
