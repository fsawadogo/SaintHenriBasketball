using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Validators;

namespace SaintHenriBasketball.Application.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ISeasonSubscriptionService, SeasonSubscriptionService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddAutoMapper(typeof(AuthService).Assembly);
        services.AddLogging();

        return services;
    }
}