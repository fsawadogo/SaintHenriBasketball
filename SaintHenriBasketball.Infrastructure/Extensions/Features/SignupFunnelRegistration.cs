using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `signup-funnel` feature.
public static class SignupFunnelRegistration
{
    public static IServiceCollection AddSignupFunnelFeature(this IServiceCollection services)
    {
        services.AddScoped<ISignupFunnelRepository, SignupFunnelRepository>();
        services.AddScoped<ISignupFunnelService, SignupFunnelService>();
        return services;
    }
}
