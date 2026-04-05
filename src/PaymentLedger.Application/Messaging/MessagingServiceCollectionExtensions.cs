using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace PaymentLedger.Application.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the dispatcher, every request handler found in <paramref name="assembly"/>,
    /// and the given open-generic pipeline behaviours (outermost first).
    /// </summary>
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        Assembly assembly,
        params Type[] pipelineBehaviors)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false });

        foreach (var type in handlerTypes)
        {
            foreach (var contract in type.GetInterfaces())
            {
                if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
                {
                    services.AddTransient(contract, type);
                }
            }
        }

        foreach (var behavior in pipelineBehaviors)
        {
            services.AddTransient(typeof(IPipelineBehavior<,>), behavior);
        }

        return services;
    }
}
