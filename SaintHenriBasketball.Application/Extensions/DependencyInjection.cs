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
        services.AddAutoMapper(cfg => { }, Assembly.GetExecutingAssembly());

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ISeasonService, SeasonService>();
        services.AddScoped<IEmailAutomationService, EmailAutomationService>();
        services.AddScoped<ICacheService, MemoryCacheService>();
        services.AddScoped<IStripeService, StripeService>();
        services.AddScoped<IWaitlistService, WaitlistService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();
        services.AddScoped<ICalendarSyncService, CalendarSyncService>();
        services.AddScoped<ITwoFactorService, TwoFactorService>();

        services.AddLogging();

        return services;
    }
}