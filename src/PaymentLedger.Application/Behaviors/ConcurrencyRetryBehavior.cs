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
    public const int MaxAttempts = 8;

    private const int BaseDelayMs = 5;
    private const int MaxDelayMs = 250;

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

    // Capped exponential backoff with full jitter: the window doubles each attempt (5, 10, 20 … 250 ms)
    // so competing writers spread out instead of retrying in lockstep. A linear 10 ms step with only
    // five attempts was exhausted under real contention on a slower Postgres (Docker Desktop on
    // Windows), surfacing 409s for deposits that should simply have waited their turn.
    internal static TimeSpan Backoff(int attempt)
    {
        var ceiling = Math.Min(MaxDelayMs, BaseDelayMs << Math.Min(attempt, 16));
        return TimeSpan.FromMilliseconds(Random.Shared.Next(1, ceiling + 1));
    }
}
