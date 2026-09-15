namespace SaintHenriBasketball.Application.Services.Interfaces;

/// Closing and reopening player accounts without losing payment or attendance records.
public interface IAccountLifecycleService
{
    /// Blocks sign-in and releases upcoming reservations. With <paramref name="anonymize"/>, also erases
    /// personal details (Quebec Law 25 deletion request); that part can't be undone.
    Task DeactivateAsync(Guid userId, bool anonymize);

    Task ReactivateAsync(Guid userId);

    Task<int> CountActiveAdminsAsync();
}
