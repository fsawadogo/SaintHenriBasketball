using System.Security.Claims;

namespace SaintHenriBasketball.API.Extensions;

/// Who performed an action, for audit log entries.
public static class ClaimsPrincipalAuditExtensions
{
    public static Guid? AuditUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static string AuditUserName(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue(ClaimTypes.Email) ?? "Admin";
}
