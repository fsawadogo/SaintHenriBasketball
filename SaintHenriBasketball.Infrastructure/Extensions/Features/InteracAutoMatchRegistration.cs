using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `interac-auto-match` feature.
public static class InteracAutoMatchRegistration
{
    public static IServiceCollection AddInteracAutoMatchFeature(this IServiceCollection services)
    {
        services.AddScoped<IInteracDepositRepository, InteracDepositRepository>();
        services.AddScoped<IInteracDepositService, InteracDepositService>();
        return services;
    }
}
