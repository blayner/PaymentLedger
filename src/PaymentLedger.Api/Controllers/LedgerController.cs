using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Ledger.Queries;
using PaymentLedger.Application.Messaging;

namespace PaymentLedger.Api.Controllers;

[Route("api/ledger")]
public sealed class LedgerController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpGet("accounts/{accountId:guid}/entries")]
    [ProducesResponseType(typeof(PagedResult<LedgerEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<LedgerEntryDto>>> GetAccountEntries(
        Guid accountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        await dispatcher.Send(new GetLedgerEntriesQuery(accountId, page, pageSize), cancellationToken);

    [HttpGet("transactions/{transactionId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<LedgerEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LedgerEntryDto>>> GetTransaction(
        Guid transactionId,
        CancellationToken cancellationToken) =>
        Ok(await dispatcher.Send(new GetTransactionEntriesQuery(transactionId), cancellationToken));

    [HttpGet("trial-balance")]
    [ProducesResponseType(typeof(TrialBalanceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrialBalanceDto>> GetTrialBalance(CancellationToken cancellationToken) =>
        await dispatcher.Send(new GetTrialBalanceQuery(), cancellationToken);
}
