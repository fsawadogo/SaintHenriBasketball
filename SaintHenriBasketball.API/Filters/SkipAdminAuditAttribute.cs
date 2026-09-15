namespace SaintHenriBasketball.API.Filters;

/// For admin endpoints whose audit entry is written later, outside the request (for example by a background worker),
/// so AdminMutationAuditFilter doesn't add a second, less detailed entry.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class SkipAdminAuditAttribute : Attribute
{
}
