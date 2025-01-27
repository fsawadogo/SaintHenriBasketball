using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string? to, string subject, string htmlContent);
    Task SendConfirmationEmailAsync(string? to, string confirmationLink);
    Task SendPasswordResetEmailAsync(string? to, string resetLink);
    Task SendPaymentConfirmationAsync(string? email, decimal amount, string? reference, EmailLanguage language = EmailLanguage.English);
    Task SendAttendanceConfirmationEmailAsync(SessionAttendance attendance);
    Task SendAttendanceUpdateEmailAsync(SessionAttendance attendance);
    Task SendSeasonRegistrationConfirmationEmailAsync(SeasonRegistration registration);
    Task SendSeasonRegistrationCancelledEmailAsync(string? userEmail, Season season);
    Task SendSeasonStatusUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers);
    Task SendSeasonUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers, string[] changedProperties);
    Task SendSeasonPaymentReminderEmailAsync(SeasonRegistration registration);
    Task SendPaymentPlanUpdateEmailAsync(string? userEmail, string userName, PaymentPlan paymentPlan);
    Task SendNewUserNotificationToAdminAsync(ApplicationUser newUser);
    Task SendAttendanceReminderEmailAsync(string? userEmail, string userName, string? customMessage = null);
    Task SendSeasonRegistrationReminderEmailAsync(string? userEmail, string userName, string? customMessage = null);
    Task SendGeneralAnnouncementEmailAsync(string? userEmail, string userName, string message);
    Task SendPaymentReminderEmailAsync(string? userEmail, string userName, PaymentPlan paymentPlan, string? customMessage = null);
    Task<EmailSendResult> SendTargetedEmailsAsync(EmailType emailType, List<string?> emails, EmailLanguage language, string? customMessage, string? customMessageFr);
    Task<EmailSendResult> SendPaymentRemindersAsync(List<string?> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null);
    Task<EmailSendResult> SendAttendanceRemindersAsync(List<string?> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null);
    Task<EmailSendResult> SendSeasonRegistrationRemindersAsync(List<string?> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null);
    Task<EmailSendResult> SendGeneralAnnouncementsAsync(List<string?> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null);
}