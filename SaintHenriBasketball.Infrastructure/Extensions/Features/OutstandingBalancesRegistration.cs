using Microsoft.Extensions.DependencyInjection;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `outstanding-balances` feature.
public static class OutstandingBalancesRegistration
{
    public static IServiceCollection AddOutstandingBalancesFeature(this IServiceCollection services)
    {
        return services;
    }
}
