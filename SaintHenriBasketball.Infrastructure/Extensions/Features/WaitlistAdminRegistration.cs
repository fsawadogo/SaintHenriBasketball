using Microsoft.Extensions.DependencyInjection;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `waitlist-admin` feature.
public static class WaitlistAdminRegistration
{
    public static IServiceCollection AddWaitlistAdminFeature(this IServiceCollection services)
    {
        return services;
    }
}
