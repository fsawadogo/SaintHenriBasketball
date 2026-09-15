using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `season-rollover` feature.
public static class SeasonRolloverRegistration
{
    public static IServiceCollection AddSeasonRolloverFeature(this IServiceCollection services)
    {
        services.AddScoped<ISeasonRolloverRepository, SeasonRolloverRepository>();
        services.AddScoped<ISeasonRolloverService, SeasonRolloverService>();
        return services;
    }
}
