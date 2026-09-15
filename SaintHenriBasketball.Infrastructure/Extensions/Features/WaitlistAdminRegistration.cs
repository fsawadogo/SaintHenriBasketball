using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `waitlist-admin` feature.
public static class WaitlistAdminRegistration
{
    public static IServiceCollection AddWaitlistAdminFeature(this IServiceCollection services)
    {
        services.AddScoped<IWaitlistAdminRepository, WaitlistAdminRepository>();
        services.AddScoped<IWaitlistAdminService, WaitlistAdminService>();
        return services;
    }
}
