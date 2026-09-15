using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `outstanding-balances` feature.
public static class OutstandingBalancesRegistration
{
    public static IServiceCollection AddOutstandingBalancesFeature(this IServiceCollection services)
    {
        services.AddScoped<IOutstandingBalancesRepository, OutstandingBalancesRepository>();
        services.AddScoped<IOutstandingBalancesService, OutstandingBalancesService>();
        return services;
    }
}
