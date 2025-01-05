namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlContent);
    Task SendConfirmationEmailAsync(string to, string confirmationLink);
    Task SendPasswordResetEmailAsync(string to, string resetLink);
}