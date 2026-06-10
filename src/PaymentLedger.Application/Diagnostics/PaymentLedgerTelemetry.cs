using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PaymentLedger.Application.Diagnostics;

/// <summary>
/// Single ActivitySource and Meter for the service. Exporters subscribe by name
/// (<see cref="SourceName"/>), so this code has no dependency on OpenTelemetry itself.
/// </summary>
public static class PaymentLedgerTelemetry
{
    public const string SourceName = "PaymentLedger";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> TransactionsPosted = Meter.CreateCounter<long>(
        "ledger.transactions.posted",
        unit: "{transaction}",
        description: "Balanced transactions committed to the ledger (idempotent replays excluded).");

    public static readonly Counter<long> ConcurrencyRetries = Meter.CreateCounter<long>(
        "ledger.concurrency.retries",
        unit: "{retry}",
        description: "Commands re-executed after an optimistic concurrency conflict.");

    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        "ledger.request.duration",
        unit: "ms",
        description: "Time spent handling a command or query, including pipeline behaviours.");

    public static readonly Counter<long> OutboxPublished = Meter.CreateCounter<long>(
        "ledger.outbox.published",
        unit: "{message}",
        description: "Outbox messages handed to the publisher successfully.");

    public static readonly Counter<long> OutboxFailed = Meter.CreateCounter<long>(
        "ledger.outbox.failed",
        unit: "{message}",
        description: "Outbox publish attempts that failed and will be retried.");
}
