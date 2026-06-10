using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Behaviors;
using PaymentLedger.Application.Messaging;
using PaymentLedger.Application.Projections;
using PaymentLedger.Domain.Ledger;

namespace PaymentLedger.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Order matters: behaviours wrap each other in registration order.
        // Telemetry sees the whole request; validation runs once; only the handler is retried.
        services.AddMessaging(
            assembly,
            typeof(TelemetryBehavior<,>),
            typeof(ValidationBehavior<,>),
            typeof(ConcurrencyRetryBehavior<,>));

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddSingleton<LedgerPostingService>();

        services.AddSingleton<IProjection, AccountProjection>();
        services.AddSingleton<IProjection, LedgerProjection>();
        services.AddSingleton<IProjectionRunner, ProjectionRunner>();

        return services;
    }
}
