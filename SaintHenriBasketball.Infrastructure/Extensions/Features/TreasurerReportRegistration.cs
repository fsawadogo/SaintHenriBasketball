using Microsoft.Extensions.DependencyInjection;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `treasurer-report` feature.
public static class TreasurerReportRegistration
{
    public static IServiceCollection AddTreasurerReportFeature(this IServiceCollection services)
    {
        return services;
    }
}
