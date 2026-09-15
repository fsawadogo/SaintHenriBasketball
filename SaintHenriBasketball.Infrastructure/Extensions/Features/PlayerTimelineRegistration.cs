using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `player-timeline` feature.
public static class PlayerTimelineRegistration
{
    public static IServiceCollection AddPlayerTimelineFeature(this IServiceCollection services)
    {
        services.AddScoped<IPlayerTimelineRepository, PlayerTimelineRepository>();
        services.AddScoped<IPlayerTimelineService, PlayerTimelineService>();
        return services;
    }
}
