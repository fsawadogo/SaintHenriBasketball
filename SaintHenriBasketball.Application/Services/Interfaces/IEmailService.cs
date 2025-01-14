using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlContent);
    Task SendConfirmationEmailAsync(string to, string confirmationLink);
    Task SendPasswordResetEmailAsync(string to, string resetLink);
    Task SendAttendanceConfirmationEmailAsync(SessionAttendance attendance);
    Task SendAttendanceUpdateEmailAsync(SessionAttendance attendance);
    Task SendSeasonRegistrationConfirmationEmailAsync(SeasonRegistration registration);
    Task SendSeasonRegistrationCancelledEmailAsync(string userEmail, Season season);
    Task SendSeasonStatusUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers);
    Task SendSeasonUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers, string[] changedProperties);
    Task SendSeasonPaymentReminderEmailAsync(SeasonRegistration registration);
    Task SendPaymentPlanUpdateEmailAsync(string userEmail, string userName, PaymentPlan paymentPlan);
}