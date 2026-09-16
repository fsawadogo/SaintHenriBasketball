using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `season-schedule-wizard` feature.
public static class SeasonScheduleWizardRegistration
{
    public static IServiceCollection AddSeasonScheduleWizardFeature(this IServiceCollection services)
    {
        services.AddScoped<ISeasonScheduleRepository, SeasonScheduleRepository>();
        services.AddScoped<ISeasonScheduleService, SeasonScheduleService>();
        return services;
    }
}
