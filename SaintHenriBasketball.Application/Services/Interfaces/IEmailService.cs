using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlContent);
    Task SendConfirmationEmailAsync(string to, string confirmationLink);
    Task SendPasswordResetEmailAsync(string to, string resetLink);
    Task SendAttendanceConfirmationEmailAsync(SessionAttendance attendance);
    Task SendAttendanceUpdateEmailAsync(SessionAttendance attendance);
}