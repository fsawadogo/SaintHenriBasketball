using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

public class ApplicationUser
{
    public Guid Id { get; private set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string PasswordHash { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public bool IsAdmin { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
    public DateTime CreatedOn { get; private set; }
    public string EmailConfirmationToken { get; set; }
    public bool EmailConfirmed { get; set; }
    public string PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }
    public ICollection<SessionRegistration> SessionRegistrations { get; private set; }
    public EmailLanguage PreferredLanguage { get; set; }
    public string? AdminNotes { get; set; }
    public string? CalendarFeedToken { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? MedicalAlerts { get; set; }
    public string? PhoneNumber { get; set; }
    public bool SmsOptIn { get; set; }
    public bool SmsAnnouncementDismissed { get; set; }
    public bool SessionRemindersEnabled { get; set; } = true;
    public bool PaymentRemindersEnabled { get; set; } = true;
    public bool WaitlistAlertsEnabled { get; set; } = true;
    public bool CommunityUpdatesEnabled { get; set; } = true;
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool InAppNotificationsEnabled { get; set; } = true;
    /// Deactivated accounts can't sign in; their payments and history are kept.
    public bool IsDeactivated { get; set; }
    public DateTime? DeactivatedOn { get; set; }
    /// Set when the player's personal details were erased at their request (Quebec Law 25).
    public DateTime? AnonymizedOn { get; set; }

    private ApplicationUser() { } // For EF Core

    public ApplicationUser(string? username, string? email, string passwordHash, string firstName, string lastName, PaymentPlan paymentPlan)
    {
        Id = Guid.NewGuid();
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        PaymentPlan = paymentPlan;
        CreatedOn = DateTime.UtcNow;
        IsAdmin = false;
        EmailConfirmed = false;
        SessionRegistrations = new List<SessionRegistration>();
    }
}