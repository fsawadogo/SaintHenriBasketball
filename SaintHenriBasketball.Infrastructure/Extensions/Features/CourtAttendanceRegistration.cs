using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `court-attendance` feature.
public static class CourtAttendanceRegistration
{
    public static IServiceCollection AddCourtAttendanceFeature(this IServiceCollection services)
    {
        services.AddScoped<ICourtAttendanceRepository, CourtAttendanceRepository>();
        services.AddScoped<ICourtAttendanceService, CourtAttendanceService>();
        return services;
    }
}
