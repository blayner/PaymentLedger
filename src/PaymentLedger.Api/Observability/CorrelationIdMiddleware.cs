using System.Diagnostics;
using Serilog.Context;

namespace PaymentLedger.Api.Observability;

/// <summary>
/// Accepts an incoming <c>X-Correlation-Id</c> (or falls back to the W3C trace id), echoes it
/// on the response, and pushes it into the log context so every log line of the request
/// carries it, including lines written by the outbox for events this request produced.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault() is { Length: > 0 and <= 128 } incoming
            ? incoming
            : Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.Response.Headers[HeaderName] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
