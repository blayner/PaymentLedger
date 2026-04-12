using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Messaging;

namespace PaymentLedger.Application.Ledger.Queries;

public sealed record GetLedgerEntriesQuery(Guid AccountId, int Page = 1, int PageSize = 50)
    : IQuery<PagedResult<LedgerEntryDto>>;

public sealed record GetTransactionEntriesQuery(Guid TransactionId) : IQuery<IReadOnlyList<LedgerEntryDto>>;

public sealed class LedgerQueryHandlers(
    ILedgerReadModelStore ledger,
    IAccountReadModelStore accounts) :
    IQueryHandler<GetLedgerEntriesQuery, PagedResult<LedgerEntryDto>>,
    IQueryHandler<GetTransactionEntriesQuery, IReadOnlyList<LedgerEntryDto>>
{
    public async Task<PagedResult<LedgerEntryDto>> Handle(GetLedgerEntriesQuery request, CancellationToken cancellationToken)
    {
        _ = await accounts.GetAsync(request.AccountId, cancellationToken)
            ?? throw new NotFoundException("Account", request.AccountId);

        var page = await ledger.GetByAccountAsync(request.AccountId, request.Page, request.PageSize, cancellationToken);
        return page.Map(LedgerEntryDto.From);
    }

    public async Task<IReadOnlyList<LedgerEntryDto>> Handle(GetTransactionEntriesQuery request, CancellationToken cancellationToken)
    {
        var entries = await ledger.GetByTransactionAsync(request.TransactionId, cancellationToken);
        if (entries.Count == 0)
        {
            throw new NotFoundException("Transaction", request.TransactionId);
        }

        return entries.Select(LedgerEntryDto.From).ToArray();
    }
}
