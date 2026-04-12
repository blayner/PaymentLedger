using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentLedger.Application.Abstractions;
using PaymentLedger.Domain.Abstractions;
using PaymentLedger.Infrastructure.EventStore;
using PaymentLedger.Infrastructure.Persistence;
using PaymentLedger.Infrastructure.ReadModels;

namespace PaymentLedger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEventStore, InMemoryEventStore>();
        services.AddScoped<IAccountRepository, EventSourcedAccountRepository>();

        services.AddSingleton<IAccountReadModelStore, InMemoryAccountReadModelStore>();
        services.AddSingleton<ILedgerReadModelStore, InMemoryLedgerReadModelStore>();
        services.Configure<ProjectionOptions>(configuration.GetSection(ProjectionOptions.SectionName));
        services.AddHostedService<ProjectionHostedService>();

        return services;
    }
}
