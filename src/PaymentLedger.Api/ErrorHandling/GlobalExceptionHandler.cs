using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PaymentLedger.Application.Common;
using PaymentLedger.Domain.Abstractions;
using PaymentLedger.Domain.Common;

namespace PaymentLedger.Api.ErrorHandling;

/// <summary>
/// Translates exceptions into RFC 7807 problem responses. Domain errors carry a stable
/// <c>code</c> extension so clients can branch on it without parsing messages.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Problem type URI for a reused idempotency key (draft-ietf-httpapi-idempotency-key-header).</summary>
    public const string IdempotencyKeyReusedType = "urn:paymentledger:problem:idempotency-key-reused";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = $"{notFound.Resource} not found",
                Detail = notFound.Message,
            },
            IdempotencyKeyReusedException reused => WithCode(
                new ProblemDetails
                {
                    Type = IdempotencyKeyReusedType,
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Idempotency key reused with a different payload",
                    Detail = $"{reused.Message} Use a new key for a new operation, or resend the original payload to get the original result.",
                },
                "idempotency.key_reused"),
            DomainException domain => WithCode(
                new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Business rule violated",
                    Detail = domain.Message,
                },
                domain.Code),
            ConcurrencyException => WithCode(
                new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Concurrent modification",
                    Detail = "The account was modified by another request. Retry with the same idempotency key.",
                },
                "concurrency.conflict"),
            BadHttpRequestException badRequest => new ProblemDetails
            {
                Status = badRequest.StatusCode,
                Title = "Bad request",
                Detail = badRequest.Message,
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
            },
        };

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception processing {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Request rejected with {Status}: {Message}", problem.Status, exception.Message);
        }

        problem.Instance = httpContext.Request.Path;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            problem.GetType(),
            options: null,
            contentType: "application/problem+json",
            cancellationToken);

        return true;
    }

    private static ProblemDetails WithCode(ProblemDetails problem, string code)
    {
        problem.Extensions["code"] = code;
        return problem;
    }
}
