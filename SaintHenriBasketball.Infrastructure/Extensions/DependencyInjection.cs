using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;
using SaintHenriBasketball.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Mapping;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.Services.Implementations;

namespace SaintHenriBasketball.Infrastructure.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<ISessionRegistrationRepository, SessionRegistrationRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ISeasonRepository, SeasonRepository>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<ISessionAttendanceRepository, SessionAttendanceRepository>();
        services.AddScoped<IWaitlistRepository, WaitlistRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();
        services.AddScoped<ISessionFeedbackRepository, SessionFeedbackRepository>();
        services.AddScoped<ISessionRecapRepository, SessionRecapRepository>();
        services.AddScoped<ISessionTemplateRepository, SessionTemplateRepository>();
        services.AddScoped<IReferralRepository, ReferralRepository>();
        services.AddScoped<IPromoCodeRepository, PromoCodeRepository>();

        // Add AutoMapper
        services.AddAutoMapper(cfg => { }, typeof(MappingProfile).Assembly);

        return services;
    }
}
