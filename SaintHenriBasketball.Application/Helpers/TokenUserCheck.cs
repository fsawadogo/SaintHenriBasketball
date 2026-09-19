using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Helpers;

/// What the API needs to know about a token's user on every request.
public sealed record AuthUserSnapshot(bool IsDeactivated, bool IsAdmin, StaffRole StaffRole, bool EmailConfirmed);

public static class TokenUserCheck
{
    /// <summary>
    /// Null when a validly signed token may still be used; otherwise the reason it is refused.
    /// A promoted user gets the Admin role at their next sign-in, but a deactivated or demoted user
    /// loses access on their next request instead of when the token expires.
    /// A token whose volunteer role (<paramref name="tokenStaffRole"/>, the staff_role claim; none means None)
    /// differs from the account's is refused too, so a changed role takes effect at the next sign-in.
    /// </summary>
    public static string? Evaluate(AuthUserSnapshot? user, bool tokenClaimsAdmin, string? tokenStaffRole) => user switch
    {
        null => "Account not found",
        { IsDeactivated: true } => "Account deactivated",
        // Registration hands back a signed token before the address is confirmed, and LoginAsync is
        // the only other place that ever looked at EmailConfirmed — so that token was a working
        // session, and the confirmation step was decorative. Someone could register under another
        // person's address and act as them while that person received the confirmation mail.
        { EmailConfirmed: false } => "Email not confirmed",
        { IsAdmin: false } when tokenClaimsAdmin => "Admin access was removed",
        _ when StaffAccess.RoleFromClaim(tokenStaffRole) != user.StaffRole => "Staff role changed",
        _ => null,
    };
}
