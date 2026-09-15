using Microsoft.Extensions.DependencyInjection;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `player-timeline` feature.
public static class PlayerTimelineRegistration
{
    public static IServiceCollection AddPlayerTimelineFeature(this IServiceCollection services)
    {
        return services;
    }
}
