using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly bool _emailEnabled;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _emailEnabled = _configuration.GetValue<bool>("SendGrid:Enabled");
    }

    public async Task SendEmailAsync(string to, string subject, string htmlContent)
    {
        try
        {
            _logger.LogInformation("Attempting to send email to {Email}", to);

            var apiKey = _configuration["SendGrid:ApiKey"];
            var fromEmail = _configuration["SendGrid:FromEmail"];
            var fromName = _configuration["SendGrid:FromName"];

            _logger.LogInformation("Using configuration: FromEmail: {FromEmail}, FromName: {FromName}",
                fromEmail, fromName);

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress(fromEmail, fromName);
            var toAddress = new EmailAddress(to);
            var msg = MailHelper.CreateSingleEmail(from, toAddress, subject, null, htmlContent);

            var response = await client.SendEmailAsync(msg);
            var responseBody = await response.Body.ReadAsStringAsync();

            _logger.LogInformation("SendGrid Response: StatusCode: {StatusCode}, Body: {Body}",
                response.StatusCode, responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to send email. Status Code: {StatusCode}, Body: {Body}",
                    response.StatusCode, responseBody);
                throw new Exception($"Failed to send email. Status Code: {response.StatusCode}, Response: {responseBody}");
            }

            _logger.LogInformation("Email sent successfully to {Email}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {Email}. Error: {Error}", to, ex.Message);
            throw;
        }
    }

    public async Task SendConfirmationEmailAsync(string to, string confirmationLink)
    {
        try
        {
            _logger.LogInformation("Preparing confirmation email for {Email} with link {Link}", to, confirmationLink);

            var subject = "Confirm your email - Saint Henri Basketball";
            var htmlContent = $@"
                <html>
                <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                    <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                        <h1 style='color: #333; text-align: center;'>Welcome to Saint Henri Basketball!</h1>
                        <p style='color: #666; font-size: 16px;'>Please confirm your email address by clicking the button below:</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{confirmationLink}' style='background-color: #4CAF50; color: white; padding: 12px 25px; text-decoration: none; border-radius: 3px; display: inline-block;'>
                                Confirm Email
                            </a>
                        </div>
                        <p style='color: #666; font-size: 14px;'>If you didn't request this, you can safely ignore this email.</p>
                        <p style='color: #666; font-size: 14px;'>Or copy and paste this link in your browser:</p>
                        <p style='color: #666; font-size: 14px; word-break: break-all;'>{confirmationLink}</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                        <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(to, subject, htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send confirmation email to {Email}", to);
            throw;
        }
    }

    public async Task SendPasswordResetEmailAsync(string to, string resetLink)
    {
        try
        {
            _logger.LogInformation("Preparing password reset email for {Email}", to);

            var subject = "Reset Your Password - Saint Henri Basketball";
            var htmlContent = $@"
                <html>
                <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                    <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                        <h1 style='color: #333; text-align: center;'>Password Reset Request</h1>
                        <p style='color: #666; font-size: 16px;'>You requested to reset your password. Click the button below to reset it:</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{resetLink}' style='background-color: #4CAF50; color: white; padding: 12px 25px; text-decoration: none; border-radius: 3px; display: inline-block;'>
                                Reset Password
                            </a>
                        </div>
                        <p style='color: #666; font-size: 14px;'>If you didn't request this, please ignore this email.</p>
                        <p style='color: #666; font-size: 14px;'>This link will expire in 1 hour.</p>
                        <p style='color: #666; font-size: 14px;'>Or copy and paste this link in your browser:</p>
                        <p style='color: #666; font-size: 14px; word-break: break-all;'>{resetLink}</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                        <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(to, subject, htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", to);
            throw;
        }
    }

        public async Task SendAttendanceConfirmationEmailAsync(SessionAttendance attendance)
    {
        try
        {
            _logger.LogInformation("Preparing attendance confirmation email for session {SessionId} and user {UserId}", 
                attendance.SessionId, attendance.UserId);

            var subject = "Basketball Session Attendance Confirmation";
            var htmlContent = $@"
                <html>
                <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                    <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                        <h1 style='color: #333; text-align: center;'>Attendance Confirmation</h1>
                        <p style='color: #666; font-size: 16px;'>Your attendance has been recorded for the following session:</p>
                        <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 5px 0; color: #444;'><strong>Date:</strong> {attendance.Session.StartTime:dddd, MMMM dd, yyyy}</p>
                            <p style='margin: 5px 0; color: #444;'><strong>Time:</strong> {attendance.Session.StartTime:hh:mm tt} - {attendance.Session.EndTime:hh:mm tt}</p>
                            <p style='margin: 5px 0; color: #444;'><strong>Location:</strong> {attendance.Session.Location}</p>
                            <p style='margin: 5px 0; color: #444;'><strong>Status:</strong> {(attendance.IsAttending ? "Present" : "Absent")}</p>
                            {(!string.IsNullOrEmpty(attendance.Notes) ? $"<p style='margin: 5px 0; color: #444;'><strong>Notes:</strong> {attendance.Notes}</p>" : "")}
                        </div>
                        <p style='color: #666; font-size: 14px;'>If you believe this was recorded in error, please contact the administrator.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                        <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(attendance.User.Email, subject, htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send attendance confirmation email for session {SessionId} and user {UserId}", 
                attendance.SessionId, attendance.UserId);
            throw;
        }
    }

        public async Task SendAttendanceUpdateEmailAsync(SessionAttendance attendance)
    {
        try
        {
            _logger.LogInformation("Preparing attendance update email for session {SessionId} and user {UserId}", 
                attendance.SessionId, attendance.UserId);

            var subject = "Basketball Session Attendance Update";
            var htmlContent = $@"
                <html>
                <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                    <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                        <h1 style='color: #333; text-align: center;'>Attendance Update</h1>
                        <p style='color: #666; font-size: 16px;'>Your attendance status has been updated for the following session:</p>
                        <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 5px 0; color: #444;'><strong>Date:</strong> {attendance.Session.StartTime:dddd, MMMM dd, yyyy}</p>
                            <p style='margin: 5px 0; color: #444;'><strong>Time:</strong> {attendance.Session.StartTime:hh:mm tt} - {attendance.Session.EndTime:hh:mm tt}</p>
                            <p style='margin: 5px 0; color: #444;'><strong>Location:</strong> {attendance.Session.Location}</p>
                            <p style='margin: 5px 0; color: #444;'><strong>Updated Status:</strong> {(attendance.IsPresent ? "Present" : "Absent")}</p>
                            {(!string.IsNullOrEmpty(attendance.Notes) ? $"<p style='margin: 5px 0; color: #444;'><strong>Notes:</strong> {attendance.Notes}</p>" : "")}
                        </div>
                        <p style='color: #666; font-size: 14px;'>If you believe this update was made in error, please contact the administrator.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                        <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(attendance.User.Email, subject, htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send attendance update email for session {SessionId} and user {UserId}", 
                attendance.SessionId, attendance.UserId);
            throw;
        }
    }
}