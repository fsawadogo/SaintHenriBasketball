using Microsoft.AspNetCore.Authorization;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.API.Authorization;

/// Admin, or a volunteer holding <see cref="Role"/> while volunteer-roles is on.
public sealed class StaffRoleRequirement(StaffRole role) : IAuthorizationRequirement
{
    public StaffRole Role { get; } = role;
}

public sealed class StaffRoleAuthorizationHandler(IFeatureFlagService featureFlags) : AuthorizationHandler<StaffRoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, StaffRoleRequirement requirement)
    {
        var isAdmin = context.User.IsInRole("Admin");
        var claim = context.User.FindFirst(StaffAccess.ClaimType)?.Value;
        // Only a token claiming this role needs the flag lookup.
        var volunteerRolesEnabled = !isAdmin && StaffAccess.ClaimsRole(claim, requirement.Role)
            && await featureFlags.IsEnabledAsync(FeatureFlagKeys.VolunteerRoles);
        if (StaffAccess.IsAllowed(isAdmin, claim, requirement.Role, volunteerRolesEnabled))
            context.Succeed(requirement);
    }
}

public static class StaffRolePolicies
{
    public static void AddStaffRolePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(StaffAccess.CourtCaptainOrAdminPolicy, policy =>
            policy.RequireAuthenticatedUser().AddRequirements(new StaffRoleRequirement(StaffRole.CourtCaptain)));
        options.AddPolicy(StaffAccess.TreasurerOrAdminPolicy, policy =>
            policy.RequireAuthenticatedUser().AddRequirements(new StaffRoleRequirement(StaffRole.Treasurer)));
    }
}
