using PaymentLedger.Domain.Accounts;
using PaymentLedger.Domain.Accounts.Events;
using PaymentLedger.Domain.Common;
using PaymentLedger.Domain.Ledger;
using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.Domain.Tests;

public sealed class AccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly LedgerPostingService _posting = new();

    [Fact]
    public void Opening_raises_a_single_created_event()
    {
        var id = Guid.NewGuid();

        var account = Account.Open(id, "  Alice  ", "BRL");

        var created = account.UncommittedEvents.ShouldHaveSingleItem().ShouldBeOfType<AccountCreatedEvent>();
        created.AccountId.ShouldBe(id);
        created.OwnerName.ShouldBe("Alice");
        created.AccountType.ShouldBe(AccountType.Liability);
        created.IsSystemAccount.ShouldBeFalse();
        account.Balance.ShouldBe(Money.Zero("BRL"));
        account.Version.ShouldBe(-1);
    }

    [Fact]
    public void Rehydrating_from_history_reproduces_state()
    {
        var (customer, settlement) = OpenPair();
        Deposit(customer, settlement, 120m);
        Withdraw(customer, settlement, 20m);
        customer.Freeze("kyc review");

        var history = customer.UncommittedEvents.ToArray();
        var replayed = Account.Rehydrate(history);

        replayed.Id.ShouldBe(customer.Id);
        replayed.Balance.ShouldBe(new Money(100m, "BRL"));
        replayed.Status.ShouldBe(AccountStatus.Frozen);
        replayed.Version.ShouldBe(history.Length - 1);
    }

    [Fact]
    public void Mark_committed_advances_version_and_clears_pending_events()
    {
        var account = Account.Open(Guid.NewGuid(), "Alice", "BRL");

        account.MarkCommitted();

        account.Version.ShouldBe(0);
        account.UncommittedEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Frozen_accounts_reject_postings()
    {
        var (customer, settlement) = OpenPair();
        customer.Freeze("chargeback");

        var error = Should.Throw<DomainException>(() => Deposit(customer, settlement, 10m));

        error.Code.ShouldBe("account.invalid_status");
    }

    [Fact]
    public void Can_unfreeze_and_post_again()
    {
        var (customer, settlement) = OpenPair();
        customer.Freeze("review");
        customer.Unfreeze();

        Deposit(customer, settlement, 10m);

        customer.Balance.Amount.ShouldBe(10m);
    }

    [Fact]
    public void Only_zero_balance_accounts_can_be_closed()
    {
        var (customer, settlement) = OpenPair();
        Deposit(customer, settlement, 10m);

        Should.Throw<DomainException>(customer.Close).Code.ShouldBe("account.non_zero_balance");

        Withdraw(customer, settlement, 10m);
        customer.Close();
        customer.Status.ShouldBe(AccountStatus.Closed);
    }

    [Fact]
    public void System_accounts_cannot_be_closed()
    {
        var settlement = Account.OpenSystem(SystemAccounts.SettlementAccountId("BRL"), "Settlement BRL", "BRL");

        Should.Throw<DomainException>(settlement.Close).Code.ShouldBe("account.system_account");
    }

    [Fact]
    public void Settlement_account_ids_are_deterministic_per_currency()
    {
        SystemAccounts.SettlementAccountId("BRL").ShouldBe(SystemAccounts.SettlementAccountId("BRL"));
        SystemAccounts.SettlementAccountId("BRL").ShouldNotBe(SystemAccounts.SettlementAccountId("USD"));
    }

    private static (Account Customer, Account Settlement) OpenPair() =>
        (Account.Open(Guid.NewGuid(), "Alice", "BRL"),
         Account.OpenSystem(SystemAccounts.SettlementAccountId("BRL"), "Settlement BRL", "BRL"));

    private void Deposit(Account customer, Account settlement, decimal amount) =>
        _posting.Post(
            PostingRules.Deposit(Guid.NewGuid(), customer.Id, settlement.Id, new Money(amount, "BRL"), "deposit", Now),
            [customer, settlement]);

    private void Withdraw(Account customer, Account settlement, decimal amount) =>
        _posting.Post(
            PostingRules.Withdrawal(Guid.NewGuid(), customer.Id, settlement.Id, new Money(amount, "BRL"), "withdrawal", Now),
            [customer, settlement]);
}
