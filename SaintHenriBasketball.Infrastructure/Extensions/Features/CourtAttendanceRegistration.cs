using Microsoft.Extensions.DependencyInjection;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `court-attendance` feature.
public static class CourtAttendanceRegistration
{
    public static IServiceCollection AddCourtAttendanceFeature(this IServiceCollection services)
    {
        return services;
    }
}
