using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Helpers;

/// Volunteer staff roles: who may use a staff endpoint, the token claim, and the rules for giving a role.
/// Staff roles grant nothing while the `volunteer-roles` flag is off.
public static class StaffAccess
{
    /// JWT claim holding the StaffRole name; absent when the role is None.
    public const string ClaimType = "staff_role";

    public const string CourtCaptainOrAdminPolicy = "CourtCaptainOrAdmin";
    public const string TreasurerOrAdminPolicy = "TreasurerOrAdmin";

    /// Authorization policies that admit staff; endpoints using them are admin-level for auditing.
    public static readonly IReadOnlyList<string> Policies = new[] { CourtCaptainOrAdminPolicy, TreasurerOrAdminPolicy };

    public const string UnknownRoleMessage = "Choose a volunteer role: None, CourtCaptain or Treasurer.";
    public const string AdminRefusedMessage = "Admins already have full access, so they don't need a volunteer role.";
    public const string DeactivatedRefusedMessage = "Reactivate this player before giving them a volunteer role.";

    /// Matches a role by name (case-insensitive). Numbers and unknown names are refused.
    public static bool TryParseRole(string? value, out StaffRole role)
    {
        role = StaffRole.None;
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name)) return false;
        foreach (var candidate in Enum.GetValues<StaffRole>())
        {
            if (string.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                role = candidate;
                return true;
            }
        }
        return false;
    }

    /// The role a token claims: None without a claim, null when the claim isn't a known role.
    public static StaffRole? RoleFromClaim(string? claim) =>
        string.IsNullOrWhiteSpace(claim) ? StaffRole.None : TryParseRole(claim, out var role) ? role : null;

    /// The claim value to put in a token, or null when no claim is issued.
    public static string? ClaimValue(StaffRole role) => role == StaffRole.None ? null : role.ToString();

    /// True when the token claims exactly <paramref name="required"/> (never for None).
    public static bool ClaimsRole(string? claim, StaffRole required) =>
        required != StaffRole.None && RoleFromClaim(claim) == required;

    /// Admins always pass. Staff pass only while volunteer-roles is on and their claim is the required role.
    public static bool IsAllowed(bool isAdmin, string? staffRoleClaim, StaffRole required, bool volunteerRolesEnabled) =>
        isAdmin || (volunteerRolesEnabled && ClaimsRole(staffRoleClaim, required));

    /// Null when the role may be set; otherwise why not. Clearing a role (None) is always allowed.
    public static string? SetRoleRefusal(bool isAdmin, bool isDeactivated, StaffRole role) => role switch
    {
        StaffRole.None => null,
        _ when isAdmin => AdminRefusedMessage,
        _ when isDeactivated => DeactivatedRefusedMessage,
        _ => null,
    };

    /// Admin-only flags a staff member's screens gate on, sent to them by feature-flags/public while volunteer-roles is on.
    public static IReadOnlyList<string> ClientFlagKeys(StaffRole role) => role switch
    {
        StaffRole.CourtCaptain => new[] { FeatureFlagKeys.VolunteerRoles, FeatureFlagKeys.CourtAttendance },
        StaffRole.Treasurer => new[]
        {
            FeatureFlagKeys.VolunteerRoles, FeatureFlagKeys.TreasurerReport, FeatureFlagKeys.OutstandingBalances,
            FeatureFlagKeys.InteracReconciliation, FeatureFlagKeys.PromoReferralReports,
        },
        _ => Array.Empty<string>(),
    };
}
