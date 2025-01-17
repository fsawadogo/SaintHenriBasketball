using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IEmailService
{
    // Base email sending
    Task SendEmailAsync(string to, string subject, string htmlContent);

    // Authentication emails
    Task SendConfirmationEmailAsync(string to, string confirmationLink);
    Task SendPasswordResetEmailAsync(string to, string resetLink);

    // Attendance related emails
    Task SendAttendanceConfirmationEmailAsync(SessionAttendance attendance);
    Task SendAttendanceUpdateEmailAsync(SessionAttendance attendance);
    Task SendAttendanceReminderEmailAsync(string userEmail, string userName, string? customMessage = null);

    // Season related emails
    Task SendSeasonRegistrationConfirmationEmailAsync(SeasonRegistration registration);
    Task SendSeasonRegistrationCancelledEmailAsync(string userEmail, Season season);
    Task SendSeasonStatusUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers);
    Task SendSeasonUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers, string[] changedProperties);
    Task SendSeasonRegistrationReminderEmailAsync(string userEmail, string userName, string? customMessage = null);

    // Payment related emails
    Task SendSeasonPaymentReminderEmailAsync(SeasonRegistration registration);
    Task SendPaymentPlanUpdateEmailAsync(string userEmail, string userName, PaymentPlan paymentPlan);
    Task SendPaymentReminderEmailAsync(string userEmail, string userName, PaymentPlan paymentPlan, string? customMessage = null);

    // Admin notification emails
    Task SendNewUserNotificationToAdminAsync(ApplicationUser newUser);

    // General communication
    Task SendGeneralAnnouncementEmailAsync(string userEmail, string userName, string message);
    Task<EmailSendResult> SendTargetedEmailsAsync(EmailType emailType, List<string> emails, EmailLanguage language, string? customMessage, string? customMessageFr);
}