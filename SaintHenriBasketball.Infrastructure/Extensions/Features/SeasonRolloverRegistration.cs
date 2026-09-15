using Microsoft.Extensions.DependencyInjection;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `season-rollover` feature.
public static class SeasonRolloverRegistration
{
    public static IServiceCollection AddSeasonRolloverFeature(this IServiceCollection services)
    {
        return services;
    }
}
