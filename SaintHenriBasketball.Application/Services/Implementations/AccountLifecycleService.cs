using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class AccountLifecycleService : IAccountLifecycleService
{
    public const string AnonymizedCannotReactivateMessage = "This account's personal details were erased, so it can't be reactivated.";

    private readonly IUserRepository _users;
    private readonly ISessionRegistrationRepository _registrations;
    private readonly IParticipationRepository _participation;
    private readonly ILogger<AccountLifecycleService> _logger;

    public AccountLifecycleService(
        IUserRepository users,
        ISessionRegistrationRepository registrations,
        IParticipationRepository participation,
        ILogger<AccountLifecycleService> logger)
    {
        _users = users;
        _registrations = registrations;
        _participation = participation;
        _logger = logger;
    }

    public async Task DeactivateAsync(Guid userId, bool anonymize)
    {
        var user = await _users.GetByIdAsync(userId) ?? throw new NotFoundException("User not found");

        if (!user.IsDeactivated)
        {
            user.IsDeactivated = true;
            user.DeactivatedOn = DateTime.UtcNow;
        }

        if (anonymize && user.AnonymizedOn == null)
        {
            var tag = user.Id.ToString("N");
            user.FirstName = "Former";
            user.LastName = "player";
            user.Username = $"deleted-{tag}";
            user.Email = $"deleted-{tag}@deleted.invalid";
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            user.PasswordResetToken = null!;
            user.PasswordResetTokenExpiry = null;
            user.PhoneNumber = null;
            user.EmergencyContactName = null;
            user.EmergencyContactPhone = null;
            user.MedicalAlerts = null;
            user.AdminNotes = null;
            user.CalendarFeedToken = null;
            user.TwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            user.SmsOptIn = false;
            user.SessionRemindersEnabled = false;
            user.PaymentRemindersEnabled = false;
            user.WaitlistAlertsEnabled = false;
            user.CommunityUpdatesEnabled = false;
            user.EmailNotificationsEnabled = false;
            user.InAppNotificationsEnabled = false;
            user.AnonymizedOn = DateTime.UtcNow;
        }

        await _users.UpdateAsync(user);
        await ReleaseUpcomingReservationsAsync(user.Id);
    }

    public async Task ReactivateAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId) ?? throw new NotFoundException("User not found");
        if (user.AnonymizedOn != null)
            throw new ValidationException(AnonymizedCannotReactivateMessage);

        user.IsDeactivated = false;
        user.DeactivatedOn = null;
        await _users.UpdateAsync(user);
    }

    public Task<int> CountActiveAdminsAsync() => _users.CountActiveAdminsAsync();

    public async Task SetAdminAsync(Guid userId, bool isAdmin)
    {
        var user = await _users.GetByIdAsync(userId) ?? throw new NotFoundException("User not found");
        if (user.IsAdmin == isAdmin) return;
        user.IsAdmin = isAdmin;
        await _users.UpdateAsync(user);
    }

    /// A deactivated player's places go back to the session (and its waitlist).
    private async Task ReleaseUpcomingReservationsAsync(Guid userId)
    {
        var today = DateTime.UtcNow.Date;
        foreach (var registration in await _registrations.GetByUserIdAsync(userId))
        {
            if (registration.Session is null || registration.Session.SessionDate.Date < today) continue;
            try
            {
                await _participation.CancelAsync(registration.SessionId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not release session {SessionId} for deactivated user {UserId}", registration.SessionId, userId);
            }
        }
    }
}
