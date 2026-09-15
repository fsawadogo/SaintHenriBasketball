using Microsoft.Extensions.DependencyInjection;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `volunteer-roles` feature.
public static class VolunteerRolesRegistration
{
    public static IServiceCollection AddVolunteerRolesFeature(this IServiceCollection services)
    {
        return services;
    }
}
