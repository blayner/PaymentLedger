using System.Diagnostics;
using PaymentLedger.Application.Contracts;
using PaymentLedger.Application.Diagnostics;
using PaymentLedger.Application.Messaging;

namespace PaymentLedger.Application.Behaviors;

/// <summary>
/// Outermost behaviour: one span and one duration sample per command or query, tagged
/// with the outcome. Validation failures and retries happen inside this span, so a trace
/// shows the full cost of a request as the client experienced it.
/// </summary>
public sealed class TelemetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly string RequestName = typeof(TRequest).Name;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using var activity = PaymentLedgerTelemetry.ActivitySource.StartActivity(RequestName);
        activity?.SetTag("ledger.request.kind", RequestName.EndsWith("Query", StringComparison.Ordinal) ? "query" : "command");

        var started = Stopwatch.GetTimestamp();
        var outcome = "success";

        try
        {
            var response = await next();

            if (response is TransactionResult transaction)
            {
                activity?.SetTag("ledger.transaction_id", transaction.TransactionId);
                activity?.SetTag("ledger.idempotent_replay", transaction.AlreadyProcessed);

                if (!transaction.AlreadyProcessed)
                {
                    PaymentLedgerTelemetry.TransactionsPosted.Add(1, new KeyValuePair<string, object?>("request", RequestName));
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            outcome = ex.GetType().Name;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            PaymentLedgerTelemetry.RequestDuration.Record(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("request", RequestName),
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }
}
