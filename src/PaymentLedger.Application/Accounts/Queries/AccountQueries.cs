using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Messaging;

namespace PaymentLedger.Application.Accounts.Queries;

public sealed record GetAccountQuery(Guid AccountId) : IQuery<AccountDto>;

public sealed record ListAccountsQuery(int Page = 1, int PageSize = 50, bool IncludeSystemAccounts = false)
    : IQuery<PagedResult<AccountDto>>;

/// <summary>
/// Reads the balance from the event stream rather than the read model. Read models are
/// eventually consistent; a balance shown right before a withdrawal must not be stale.
/// </summary>
public sealed record GetAccountBalanceQuery(Guid AccountId) : IQuery<AccountBalanceDto>;

public sealed class AccountQueryHandlers(
    IAccountReadModelStore readModels,
    IAccountRepository accounts,
    TimeProvider clock) :
    IQueryHandler<GetAccountQuery, AccountDto>,
    IQueryHandler<ListAccountsQuery, PagedResult<AccountDto>>,
    IQueryHandler<GetAccountBalanceQuery, AccountBalanceDto>
{
    public async Task<AccountDto> Handle(GetAccountQuery request, CancellationToken cancellationToken)
    {
        var model = await readModels.GetAsync(request.AccountId, cancellationToken)
                    ?? throw new NotFoundException("Account", request.AccountId);
        return AccountDto.From(model);
    }

    public async Task<PagedResult<AccountDto>> Handle(ListAccountsQuery request, CancellationToken cancellationToken)
    {
        var page = await readModels.ListAsync(request.Page, request.PageSize, request.IncludeSystemAccounts, cancellationToken);
        return page.Map(AccountDto.From);
    }

    public async Task<AccountBalanceDto> Handle(GetAccountBalanceQuery request, CancellationToken cancellationToken)
    {
        var account = await accounts.GetRequiredAsync(request.AccountId, cancellationToken);
        return new AccountBalanceDto(account.Id, account.Balance.Amount, account.Currency, account.Version, clock.GetUtcNow());
    }
}
