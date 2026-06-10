using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PaymentLedger.Application.Abstractions;
using PaymentLedger.Domain.Abstractions;
using PaymentLedger.Infrastructure.EventStore;
using PaymentLedger.Infrastructure.Health;
using PaymentLedger.Infrastructure.Outbox;
using PaymentLedger.Infrastructure.Persistence;
using PaymentLedger.Infrastructure.Persistence.Postgres;
using PaymentLedger.Infrastructure.ReadModels;
using PaymentLedger.Infrastructure.Serialization;

namespace PaymentLedger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(_ => new EventSerializer());

        var persistence = configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>()
                          ?? new PersistenceOptions();

        // Event store + outbox. The schema initializer is a hosted service and must be
        // registered before the projection and outbox workers below.
        if (persistence.Provider == PersistenceProvider.Postgres)
        {
            AddPostgres(services, configuration);
        }
        else
        {
            services.AddSingleton<InMemoryOutboxStore>();
            services.AddSingleton<IOutboxStore>(sp => sp.GetRequiredService<InMemoryOutboxStore>());
            services.AddSingleton<IEventStore, InMemoryEventStore>();
        }

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

        services.AddHealthChecks()
            .AddCheck<OutboxHealthCheck>("outbox", tags: [HealthTags.Ready]);

        return services;
    }

    private static void AddPostgres(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(PersistenceOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{PersistenceOptions.ConnectionStringName} is required when Persistence:Provider is Postgres.");
        }

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddHostedService<DatabaseInitializer>();
        services.AddSingleton<IEventStore, PostgresEventStore>();
        services.AddSingleton<IOutboxStore, PostgresOutboxStore>();

        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: [HealthTags.Ready]);
    }
}
