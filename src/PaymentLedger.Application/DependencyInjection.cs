using Microsoft.Extensions.DependencyInjection;
using PaymentLedger.Application.Abstractions;
using PaymentLedger.Application.Messaging;
using PaymentLedger.Application.Projections;

namespace PaymentLedger.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMessaging(typeof(DependencyInjection).Assembly);

        services.AddSingleton<IProjection, AccountProjection>();
        services.AddSingleton<IProjection, LedgerProjection>();
        services.AddSingleton<IProjectionRunner, ProjectionRunner>();

        return services;
    }
}
