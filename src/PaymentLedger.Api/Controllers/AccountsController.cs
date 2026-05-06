using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Api.Contracts;
using PaymentLedger.Application.Accounts.Commands;
using PaymentLedger.Application.Accounts.Queries;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Messaging;

namespace PaymentLedger.Api.Controllers;

[Route("api/accounts")]
public sealed class AccountsController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var accountId = await dispatcher.Send(new CreateAccountCommand(request.OwnerName, request.Currency), cancellationToken);
        var account = await dispatcher.Send(new GetAccountQuery(accountId), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { accountId }, account);
    }

    [HttpGet("{accountId:guid}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountDto>> GetById(Guid accountId, CancellationToken cancellationToken) =>
        await dispatcher.Send(new GetAccountQuery(accountId), cancellationToken);

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AccountDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool includeSystem = false,
        CancellationToken cancellationToken = default) =>
        await dispatcher.Send(new ListAccountsQuery(page, pageSize, includeSystem), cancellationToken);

    [HttpGet("{accountId:guid}/balance")]
    [ProducesResponseType(typeof(AccountBalanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountBalanceDto>> GetBalance(Guid accountId, CancellationToken cancellationToken) =>
        await dispatcher.Send(new GetAccountBalanceQuery(accountId), cancellationToken);

    [HttpPost("{accountId:guid}/deposits")]
    [ProducesResponseType(typeof(TransactionResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(TransactionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Deposit(
        Guid accountId,
        MoneyMovementRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is not { } transactionId)
        {
            return MissingIdempotencyKey();
        }

        var result = await dispatcher.Send(
            new DepositFundsCommand(accountId, request.Amount, request.Currency, transactionId, request.Description),
            cancellationToken);

        return TransactionOutcome(result);
    }

    [HttpPost("{accountId:guid}/withdrawals")]
    [ProducesResponseType(typeof(TransactionResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(TransactionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Withdraw(
        Guid accountId,
        MoneyMovementRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is not { } transactionId)
        {
            return MissingIdempotencyKey();
        }

        var result = await dispatcher.Send(
            new WithdrawFundsCommand(accountId, request.Amount, request.Currency, transactionId, request.Description),
            cancellationToken);

        return TransactionOutcome(result);
    }

    [HttpPost("{accountId:guid}/freeze")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Freeze(Guid accountId, FreezeAccountRequest request, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new FreezeAccountCommand(accountId, request.Reason), cancellationToken);
        return NoContent();
    }

    [HttpPost("{accountId:guid}/unfreeze")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unfreeze(Guid accountId, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new UnfreezeAccountCommand(accountId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{accountId:guid}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Close(Guid accountId, CancellationToken cancellationToken)
    {
        await dispatcher.Send(new CloseAccountCommand(accountId), cancellationToken);
        return NoContent();
    }
}
