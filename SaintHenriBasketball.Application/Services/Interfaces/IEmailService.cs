using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SendGrid.Helpers.Mail;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IEmailService
{
    // Basic Email Methods
    Task SendEmailAsync(string? to, string subject, string htmlContent);
    Task SendWithRetryAsync(SendGridMessage msg, int retryCount = 0);

    // Authentication Emails
    Task SendConfirmationEmailAsync(string? to, string confirmationLink);
    Task SendPasswordResetEmailAsync(string? to, string resetLink);
    
    // Payment Related Emails
    Task SendPaymentCreatedConfirmationAsync(Guid userId, decimal amount, string? reference,
        EmailLanguage language = EmailLanguage.French);
    Task SendPaymentConfirmationAsync(Guid userId, decimal amount, string? reference, EmailLanguage language = EmailLanguage.French);
    Task SendPaymentReminderEmailAsync(Guid userId, PaymentPlan paymentPlan, string? customMessage = null);
    Task SendPaymentPlanUpdateEmailAsync(Guid userId, PaymentPlan paymentPlan);
    
    // Attendance Related Emails
    Task SendAttendanceConfirmationEmailAsync(SessionAttendance attendance);
    Task SendAttendanceUpdateEmailAsync(SessionAttendance attendance);
    Task SendAttendanceReminderEmailAsync(Guid userId, string? customMessage = null);
    
    // Season Related Emails
    Task SendSeasonRegistrationConfirmationEmailAsync(SeasonRegistration registration);
    Task SendSeasonRegistrationCancelledEmailAsync(string? userEmail, Season season);
    Task SendSeasonRegistrationReminderEmailAsync(string? userEmail, string userName, string? customMessage = null);
    Task SendSeasonStatusUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers);
    Task SendSeasonUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers, string[] changedProperties);
    Task SendSeasonPaymentReminderEmailAsync(SeasonRegistration registration);
    
    // Admin Notifications
    Task SendNewUserNotificationToAdminAsync(ApplicationUser newUser);
    
    // General Announcements
    Task SendGeneralAnnouncementEmailAsync(string? userEmail, string userName, string message);
    
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