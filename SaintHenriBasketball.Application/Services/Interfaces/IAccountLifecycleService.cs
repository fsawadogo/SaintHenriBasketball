namespace SaintHenriBasketball.Application.Services.Interfaces;

/// Closing and reopening player accounts without losing payment or attendance records.
public interface IAccountLifecycleService
{
    /// Blocks sign-in and releases upcoming reservations. With <paramref name="anonymize"/>, also erases
    /// personal details (Quebec Law 25 deletion request); that part can't be undone.
    Task DeactivateAsync(Guid userId, bool anonymize);

    Task ReactivateAsync(Guid userId);

    Task<int> CountActiveAdminsAsync();

    /// Grants or removes admin access. Callers enforce who may do this (not yourself, not the last admin).
    Task SetAdminAsync(Guid userId, bool isAdmin);

    /// Turns off a player's two-factor authentication so they can set it up again (lost phone).
    Task ResetTwoFactorAsync(Guid userId);
}
