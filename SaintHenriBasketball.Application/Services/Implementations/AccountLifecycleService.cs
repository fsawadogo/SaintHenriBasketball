using SaintHenriBasketball.Application.Helpers;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
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
    private readonly ICacheService _cache;
    private readonly IEmailService _email;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountLifecycleService> _logger;

    public AccountLifecycleService(
        IUserRepository users,
        ISessionRegistrationRepository registrations,
        IParticipationRepository participation,
        ICacheService cache,
        IEmailService email,
        IConfiguration configuration,
        ILogger<AccountLifecycleService> logger)
    {
        _email = email;
        _configuration = configuration;
        _cache = cache;
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

    public async Task<bool> ConfirmEmailAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId) ?? throw new NotFoundException("User not found");
        if (user.EmailConfirmed) return false;

        user.EmailConfirmed = true;
        // The token is spent either way: leaving a live one lets the old link confirm again later.
        user.EmailConfirmationToken = "";
        await _users.UpdateAsync(user);

        _logger.LogInformation("Email confirmed by an admin for user {UserId}", userId);
        return true;
    }

    public async Task<bool> ResendConfirmationAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId) ?? throw new NotFoundException("User not found");
        if (user.EmailConfirmed) return false;
        if (string.IsNullOrWhiteSpace(user.Email)) throw new ValidationException("This player has no email address.");

        // A fresh token, so an old link that may have leaked cannot still be used.
        user.EmailConfirmationToken = Guid.NewGuid().ToString("N");
        await _users.UpdateAsync(user);

        var appUrl = (_configuration["AppUrl"] ?? "https://sainthenribasketball.com").TrimEnd('/');
        var link = $"{appUrl}/confirm-email?token={user.EmailConfirmationToken}&email={Uri.EscapeDataString(user.Email)}";
        await _email.SendConfirmationEmailAsync(user.Email, link);

        _logger.LogInformation("Confirmation email resent for user {UserId}", userId);
        return true;
    }

    public Task<int> CountActiveAdminsAsync() => _users.CountActiveAdminsAsync();

    public async Task SetAdminAsync(Guid userId, bool isAdmin)
    {
        var user = await _users.GetByIdAsync(userId) ?? throw new NotFoundException("User not found");
        if (user.IsAdmin == isAdmin) return;
        user.IsAdmin = isAdmin;
        await _users.UpdateAsync(user);
    }

    public async Task ResetTwoFactorAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId) ?? throw new NotFoundException("User not found");
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
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
                await SessionCacheKeys.InvalidateAsync(_cache, registration.SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not release session {SessionId} for deactivated user {UserId}", registration.SessionId, userId);
            }
        }
        await _cache.RemoveAsync($"Attendance:User:{userId}");
        await _cache.RemoveAsync(SessionCacheKeys.UpcomingSessions);
        await _cache.RemoveAsync(SessionCacheKeys.AvailableSessions);
    }
}
