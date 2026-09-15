using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `promo-referral-reports` feature.
public static class PromoReferralReportsRegistration
{
    public static IServiceCollection AddPromoReferralReportsFeature(this IServiceCollection services)
    {
        services.AddScoped<IPromoReferralReportRepository, PromoReferralReportRepository>();
        services.AddScoped<IPromoReferralReportService, PromoReferralReportService>();
        services.AddScoped<IReferralCodeAdminService, ReferralCodeAdminService>();
        return services;
    }
}
