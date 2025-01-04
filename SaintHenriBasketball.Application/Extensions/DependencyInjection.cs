using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.Services.Implementations;

namespace SaintHenriBasketball.Application.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IRegistrationService, RegistrationService>();

        return services;
    }
}