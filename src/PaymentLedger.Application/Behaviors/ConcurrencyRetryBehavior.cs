using Microsoft.Extensions.Logging;
using PaymentLedger.Application.Common;
using PaymentLedger.Application.Diagnostics;
using PaymentLedger.Application.Messaging;
using PaymentLedger.Domain.Abstractions;

namespace PaymentLedger.Application.Behaviors;

/// <summary>
/// Re-executes <see cref="IRetryOnConcurrency"/> commands when the event store rejects
/// an append because the stream moved on. Hot streams (the per-currency settlement
/// account in particular) see frequent, short-lived conflicts; a few jittered retries
/// resolve them without surfacing a 409 to the client.
/// </summary>
public sealed class ConcurrencyRetryBehavior<TRequest, TResponse>(
    ILogger<ConcurrencyRetryBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public const int MaxAttempts = 5;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IRetryOnConcurrency)
        {
            return await next();
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await next();
            }
            catch (ConcurrencyException ex) when (attempt < MaxAttempts)
            {
                var delay = Backoff(attempt);
                PaymentLedgerTelemetry.ConcurrencyRetries.Add(1, new KeyValuePair<string, object?>("request", typeof(TRequest).Name));
                logger.LogInformation(
                    "Concurrency conflict on stream {StreamId} handling {Request} (attempt {Attempt}/{Max}); retrying in {Delay} ms",
                    ex.StreamId,
                    typeof(TRequest).Name,
                    attempt,
                    MaxAttempts,
                    delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    // Linear backoff with full jitter keeps competing writers from retrying in lockstep.
    private static TimeSpan Backoff(int attempt) =>
        TimeSpan.FromMilliseconds(Random.Shared.Next(1, 10 * attempt + 1));
}
