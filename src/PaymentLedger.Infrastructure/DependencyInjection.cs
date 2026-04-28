using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentLedger.Application.Abstractions;
using PaymentLedger.Domain.Abstractions;
using PaymentLedger.Infrastructure.EventStore;
using PaymentLedger.Infrastructure.Outbox;
using PaymentLedger.Infrastructure.Persistence;
using PaymentLedger.Infrastructure.ReadModels;
using PaymentLedger.Infrastructure.Serialization;

namespace PaymentLedger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(_ => new EventSerializer());

        // Event store + outbox (in-memory: both share one lock for atomicity)
        services.AddSingleton<InMemoryOutboxStore>();
        services.AddSingleton<IOutboxStore>(sp => sp.GetRequiredService<InMemoryOutboxStore>());
        services.AddSingleton<IEventStore, InMemoryEventStore>();
        services.AddScoped<IAccountRepository, EventSourcedAccountRepository>();

        // Read side
        services.AddSingleton<IAccountReadModelStore, InMemoryAccountReadModelStore>();
        services.AddSingleton<ILedgerReadModelStore, InMemoryLedgerReadModelStore>();
        services.Configure<ProjectionOptions>(configuration.GetSection(ProjectionOptions.SectionName));
        services.AddHostedService<ProjectionHostedService>();

        // Outbox dispatch
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddSingleton<IIntegrationEventPublisher, LoggingIntegrationEventPublisher>();
        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}
