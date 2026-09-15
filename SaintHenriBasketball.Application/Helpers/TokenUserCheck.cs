namespace SaintHenriBasketball.Application.Helpers;

/// What the API needs to know about a token's user on every request.
public sealed record AuthUserSnapshot(bool IsDeactivated, bool IsAdmin);

public static class TokenUserCheck
{
    /// <summary>
    /// Null when a validly signed token may still be used; otherwise the reason it is refused.
    /// A promoted user gets the Admin role at their next sign-in, but a deactivated or demoted user
    /// loses access on their next request instead of when the token expires.
    /// </summary>
    public static string? Evaluate(AuthUserSnapshot? user, bool tokenClaimsAdmin) => user switch
    {
        null => "Account not found",
        { IsDeactivated: true } => "Account deactivated",
        { IsAdmin: false } when tokenClaimsAdmin => "Admin access was removed",
        _ => null,
    };
}
