using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentLedger.Application.Abstractions;
using PaymentLedger.Domain.Abstractions;
using PaymentLedger.Infrastructure.EventStore;
using PaymentLedger.Infrastructure.Persistence;

namespace PaymentLedger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEventStore, InMemoryEventStore>();
        services.AddScoped<IAccountRepository, EventSourcedAccountRepository>();

        return services;
    }
}
