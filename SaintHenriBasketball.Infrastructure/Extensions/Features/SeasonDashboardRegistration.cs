using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `season-dashboard` feature.
public static class SeasonDashboardRegistration
{
    public static IServiceCollection AddSeasonDashboardFeature(this IServiceCollection services)
    {
        services.AddScoped<ISeasonDashboardRepository, SeasonDashboardRepository>();
        services.AddScoped<ISeasonDashboardService, SeasonDashboardService>();
        return services;
    }
}
