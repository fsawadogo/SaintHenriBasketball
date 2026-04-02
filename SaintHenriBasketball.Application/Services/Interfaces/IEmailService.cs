using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.DTOs.Session;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Services.Interfaces;
public interface IEmailService
{
    // Basic Email Methods
    Task SendEmailAsync(string? to, string subject, string htmlContent);
    Task SendEmailWithAttachmentAsync(string? to, string subject, string htmlContent, string attachmentFilename, byte[] attachmentContent);

    // Authentication Emails
    Task SendConfirmationEmailAsync(string? to, string confirmationLink);
    Task SendPasswordResetEmailAsync(string? to, string resetLink);
    Task SendAccountCreatedEmailAsync(string? to, string password, string loginLink);

    // Payment Related Emails
    Task SendPaymentCreatedConfirmationAsync(Guid userId, decimal amount, string? reference,
        EmailLanguage language = EmailLanguage.French);
    Task SendPaymentConfirmationAsync(Guid userId, decimal amount, string? reference, EmailLanguage language = EmailLanguage.French);
    Task SendPaymentReminderEmailAsync(Guid userId, PaymentPlan paymentPlan, string? customMessage = null);
    Task SendPaymentPlanUpdateEmailAsync(Guid userId, PaymentPlan newPaymentPlan);

    // Attendance Related Emails
    Task SendAttendanceConfirmationEmailAsync(SessionAttendance attendance);
    Task SendAttendanceUpdateEmailAsync(SessionAttendance attendance, bool previousStatus, string? reason = null);
    Task SendAttendanceReminderEmailAsync(Guid userId, string? customMessage = null);
    Task SendLowAttendanceWarningEmailAsync(SessionDto session, List<UserDto> registeredUsers);

    // Session Related Emails
    Task SendSessionCancellationEmailAsync(Session session, List<ApplicationUser> registeredUsers, string? cancellationReason = null, Session? alternativeSession = null);

    // Season Related Emails
    Task SendSeasonRegistrationConfirmationEmailAsync(SeasonRegistration registration);
    Task SendSeasonRegistrationCancelledEmailAsync(string? userEmail, Season season);
    Task SendSeasonRegistrationReminderEmailAsync(string? userEmail, Season season, string? customMessage = null);
    Task SendSeasonStatusUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers);
    Task SendSeasonUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers, string[] changedProperties);
    Task SendSeasonPaymentReminderEmailAsync(SeasonRegistration registration);

    // Admin Notifications
    Task SendNewUserNotificationToAdminAsync(ApplicationUser newUser);
    Task SendAdminNotificationAsync(string subject, string message, string? actionLink = null, string? actionText = null);

    // General Communications
    Task SendGeneralAnnouncementEmailAsync(string? userEmail, string userName, string message);
    Task SendScheduleChangeEmailAsync(Session session, List<ApplicationUser> affectedUsers, string details, DateTime? newDate = null, TimeSpan? newTime = null);
    Task SendFacilityUpdateEmailAsync(string? userEmail, string facilityName, string updateDetails, DateTime effectiveDate, string? alternativeFacility = null);

    // Drop-In Payment Emails
    Task SendDropInPaymentLinkEmailAsync(string? userEmail, string userName, Guid sessionId,
        decimal amount, DateTime sessionDate, string startTime, string endTime,
        EmailLanguage language = EmailLanguage.Bilingual);

    // Bulk Email Methods
    Task<EmailSendResult> SendTargetedEmailsAsync(EmailType emailType,
        List<string> emails,
        EmailLanguage language,
        string? customMessage,
        string? customMessageFr);

    Task<EmailSendResult> SendPaymentRemindersAsync(
        List<string?> emails,
        EmailLanguage language,
        string? customMessage = null,
        string? customMessageFr = null);

    Task<EmailSendResult> SendAttendanceRemindersAsync(
        List<string?> emails,
        EmailLanguage language,
        string? customMessage = null,
        string? customMessageFr = null);

    Task<EmailSendResult> SendSeasonRegistrationRemindersAsync(
        List<string?> emails,
        EmailLanguage language,
        string? customMessage = null,
        string? customMessageFr = null);

    Task<EmailSendResult> SendGeneralAnnouncementsAsync(
        List<string?> emails,
        EmailLanguage language,
        string? customMessage = null,
        string? customMessageFr = null);
}