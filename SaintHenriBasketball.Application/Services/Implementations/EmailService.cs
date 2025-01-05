using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.Services.Interfaces;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlContent)
    {
        var client = new SendGridClient(_configuration["SendGrid:ApiKey"]);
        var from = new EmailAddress(_configuration["SendGrid:FromEmail"], _configuration["SendGrid:FromName"]);
        var msg = MailHelper.CreateSingleEmail(from, new EmailAddress(to), subject, null, htmlContent);
        
        var response = await client.SendEmailAsync(msg);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to send email to {Email}", to);
            throw new Exception("Failed to send email");
        }
    }

    public async Task SendConfirmationEmailAsync(string to, string confirmationLink)
    {
        var subject = "Confirm your email - Saint Henri Basketball";
        var htmlContent = $@"
            <h1>Welcome to Saint Henri Basketball!</h1>
            <p>Please confirm your email address by clicking the link below:</p>
            <p><a href='{confirmationLink}'>Confirm Email</a></p>
            <p>If you didn't request this, please ignore this email.</p>";

        await SendEmailAsync(to, subject, htmlContent);
    }

    public async Task SendPasswordResetEmailAsync(string to, string resetLink)
    {
        var subject = "Reset Your Password - Saint Henri Basketball";
        var htmlContent = $@"
            <h1>Password Reset Request</h1>
            <p>You requested to reset your password. Click the link below to reset it:</p>
            <p><a href='{resetLink}'>Reset Password</a></p>
            <p>If you didn't request this, please ignore this email.</p>
            <p>This link will expire in 1 hour.</p>";

        await SendEmailAsync(to, subject, htmlContent);
    }
}