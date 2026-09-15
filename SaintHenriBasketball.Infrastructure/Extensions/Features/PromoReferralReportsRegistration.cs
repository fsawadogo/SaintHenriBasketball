using Microsoft.Extensions.DependencyInjection;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `promo-referral-reports` feature.
public static class PromoReferralReportsRegistration
{
    public static IServiceCollection AddPromoReferralReportsFeature(this IServiceCollection services)
    {
        return services;
    }
}
