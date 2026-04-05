using Microsoft.Extensions.DependencyInjection;
using PaymentLedger.Application.Messaging;

namespace PaymentLedger.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMessaging(typeof(DependencyInjection).Assembly);

        return services;
    }
}
