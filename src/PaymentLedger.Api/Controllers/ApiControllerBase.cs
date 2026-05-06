using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Application.Contracts;

namespace PaymentLedger.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// Money-moving endpoints require an idempotency key. It becomes the transaction id,
    /// so a client that times out and retries can never post the same payment twice.
    /// </summary>
    protected ObjectResult MissingIdempotencyKey() =>
        Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Missing idempotency key",
            detail: $"The '{IdempotencyKeyHeader}' header (a UUID) is required for operations that move money.");

    /// <summary>201 for a newly posted transaction, 200 for a replayed one.</summary>
    protected IActionResult TransactionOutcome(TransactionResult result) =>
        result.AlreadyProcessed ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
}
