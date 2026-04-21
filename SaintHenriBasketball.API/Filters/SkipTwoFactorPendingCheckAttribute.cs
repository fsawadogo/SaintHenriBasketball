namespace SaintHenriBasketball.API.Filters;

/// Marks an action as reachable even when the caller's JWT still carries the
/// `2fa_pending` claim. Used for the 2FA setup/verify endpoints themselves.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class SkipTwoFactorPendingCheckAttribute : Attribute
{
}
