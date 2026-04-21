using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Messaging;

namespace PaymentLedger.Application.Ledger.Queries;

/// <summary>
/// Trial balance: for each currency, the sum of all debits must equal the sum of all
/// credits. A non-zero difference means a posting bypassed the double-entry engine
/// and is the first thing reconciliation should alert on.
/// </summary>
public sealed record GetTrialBalanceQuery : IQuery<TrialBalanceDto>;

public sealed record TrialBalanceLine(string Currency, decimal TotalDebits, decimal TotalCredits, long EntryCount)
{
    public decimal Difference => TotalDebits - TotalCredits;

    public bool IsBalanced => Difference == 0m;
}

public sealed record TrialBalanceDto(IReadOnlyList<TrialBalanceLine> Lines, DateTimeOffset GeneratedAt)
{
    public bool IsBalanced => Lines.All(l => l.IsBalanced);
}

public sealed class GetTrialBalanceQueryHandler(ILedgerReadModelStore ledger, TimeProvider clock)
    : IQueryHandler<GetTrialBalanceQuery, TrialBalanceDto>
{
    public async Task<TrialBalanceDto> Handle(GetTrialBalanceQuery request, CancellationToken cancellationToken)
    {
        var totals = await ledger.GetTotalsAsync(cancellationToken);

        var lines = totals
            .OrderBy(t => t.Currency, StringComparer.Ordinal)
            .Select(t => new TrialBalanceLine(t.Currency, t.TotalDebits, t.TotalCredits, t.EntryCount))
            .ToArray();

        return new TrialBalanceDto(lines, clock.GetUtcNow());
    }
}
