namespace SaintHenriBasketball.Application.Helpers;

public record AdminAuditEntry(string Action, string EntityType, Guid? EntityId, string Details);

/// Which admin requests get an automatic audit entry, and how that entry is described.
public static class AdminAuditPolicy
{
    private const int MaxDetailsLength = 500;
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };
    private static readonly HashSet<string> IgnoredRouteKeys = new(StringComparer.OrdinalIgnoreCase) { "controller", "action", "version" };

    public static bool IsMutating(string method) => MutatingMethods.Contains(method);

    /// True when any [Authorize] on the endpoint requires the Admin role.
    public static bool RequiresAdmin(IEnumerable<string?> roleLists) =>
        roleLists.Any(roles => roles != null && roles
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains("Admin", StringComparer.OrdinalIgnoreCase));

    /// Successful admin-only changes only; reads and failed requests aren't recorded.
    public static bool ShouldAudit(string method, bool requiresAdmin, int statusCode) =>
        requiresAdmin && IsMutating(method) && statusCode is >= 200 and < 400;

    /// The entity is the last id in the route (a recap within a session is the recap).
    public static AdminAuditEntry Describe(string controller, string action, IEnumerable<KeyValuePair<string, string?>> routeValues, string method, string path)
    {
        Guid? entityId = null;
        foreach (var (key, value) in routeValues)
        {
            if (!IgnoredRouteKeys.Contains(key) && Guid.TryParse(value, out var id)) entityId = id;
        }
        var details = $"{method.ToUpperInvariant()} {path}";
        return new AdminAuditEntry(action, controller, entityId, details.Length > MaxDetailsLength ? details[..MaxDetailsLength] : details);
    }
}
