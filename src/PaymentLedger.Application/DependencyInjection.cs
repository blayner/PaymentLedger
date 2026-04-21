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
        services.AddMessaging(typeof(DependencyInjection).Assembly, typeof(ConcurrencyRetryBehavior<,>));

        services.AddSingleton<LedgerPostingService>();

        services.AddSingleton<IProjection, AccountProjection>();
        services.AddSingleton<IProjection, LedgerProjection>();
        services.AddSingleton<IProjectionRunner, ProjectionRunner>();

        return services;
    }
}
