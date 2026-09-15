using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `treasurer-report` feature.
public static class TreasurerReportRegistration
{
    public static IServiceCollection AddTreasurerReportFeature(this IServiceCollection services)
    {
        services.AddScoped<ITreasurerReportRepository, TreasurerReportRepository>();
        services.AddScoped<ITreasurerReportService, TreasurerReportService>();
        return services;
    }
}
