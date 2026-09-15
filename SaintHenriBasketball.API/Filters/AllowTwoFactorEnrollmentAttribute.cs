namespace SaintHenriBasketball.API.Filters;

/// Endpoints an admin can use while their session is limited to setting up two-factor authentication
/// (token claim `2fa_enroll`): 2FA setup and confirm, the current user, and public feature flags.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class AllowTwoFactorEnrollmentAttribute : Attribute
{
}
