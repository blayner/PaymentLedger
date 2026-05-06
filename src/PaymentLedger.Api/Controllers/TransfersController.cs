using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Api.Contracts;
using PaymentLedger.Application.Accounts.Commands;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Messaging;

namespace PaymentLedger.Api.Controllers;

[Route("api/transfers")]
public sealed class TransfersController(IDispatcher dispatcher) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(TransactionResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(TransactionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Transfer(
        TransferRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is not { } transactionId)
        {
            return MissingIdempotencyKey();
        }

        var result = await dispatcher.Send(
            new TransferFundsCommand(
                request.SourceAccountId,
                request.DestinationAccountId,
                request.Amount,
                request.Currency,
                transactionId,
                request.Description),
            cancellationToken);

        return TransactionOutcome(result);
    }
}
