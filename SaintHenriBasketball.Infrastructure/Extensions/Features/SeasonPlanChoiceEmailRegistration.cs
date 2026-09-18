using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services for the `season-plan-choice-email` feature.
public static class SeasonPlanChoiceEmailRegistration
{
    public static IServiceCollection AddSeasonPlanChoiceEmailFeature(this IServiceCollection services)
    {
        services.AddScoped<ISeasonPlanChoiceEmailService, SeasonPlanChoiceEmailService>();
        return services;
    }
}
