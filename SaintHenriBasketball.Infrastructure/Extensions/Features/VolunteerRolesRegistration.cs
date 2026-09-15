using Microsoft.Extensions.DependencyInjection;
using SaintHenriBasketball.Application.Services.Implementations;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Repositories;

namespace SaintHenriBasketball.Infrastructure.Extensions.Features;

/// Services and repositories for the `volunteer-roles` feature.
public static class VolunteerRolesRegistration
{
    public static IServiceCollection AddVolunteerRolesFeature(this IServiceCollection services)
    {
        services.AddScoped<IVolunteerRolesRepository, VolunteerRolesRepository>();
        services.AddScoped<IStaffRoleService, StaffRoleService>();
        services.AddScoped<ICourtCaptainService, CourtCaptainService>();
        return services;
    }
}
