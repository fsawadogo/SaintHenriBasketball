using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Application.DTOs.Users;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IUserRepository _userRepository;
    private readonly bool _emailEnabled;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IUserRepository userRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _emailEnabled = _configuration.GetValue<bool>("SendGrid:Enabled");
        _userRepository = userRepository;
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
                throw new Exception(
                    $"Failed to send email. Status Code: {response.StatusCode}, Response: {responseBody}");
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
            _logger.LogError(ex,
                "Failed to send attendance confirmation email for session {SessionId} and user {UserId}",
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

    public async Task SendSeasonRegistrationConfirmationEmailAsync(SeasonRegistration registration)
    {
        try
        {
            _logger.LogInformation("Preparing season registration confirmation email for user {UserId}",
                registration.UserId);

            var subject = "Season Registration Confirmation - Saint Henri Basketball";
            var htmlContent = $@"
            <html>
            <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                    <h1 style='color: #333; text-align: center;'>Season Registration Confirmed!</h1>
                    <p style='color: #666; font-size: 16px;'>Thank you for registering for the basketball season!</p>
                    
                    <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p style='margin: 5px 0; color: #444;'><strong>Season Period:</strong> {registration.Season.StartDate:MMM dd, yyyy} - {registration.Season.EndDate:MMM dd, yyyy}</p>
                        <p style='margin: 5px 0; color: #444;'><strong>Registration Date:</strong> {registration.RegisteredOn:MMM dd, yyyy}</p>
                        <p style='margin: 5px 0; color: #444;'><strong>Season Price:</strong> ${registration.Season.Price}</p>
                    </div>

                    <div style='background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <h2 style='color: #2e7d32; margin-top: 0;'>Payment Instructions</h2>
                        <p style='color: #444; margin: 5px 0;'><strong>Please complete your payment using Interac e-Transfer:</strong></p>
                        <ul style='color: #444; margin: 10px 0;'>
                            <li>Send to: <strong>pay@sainthenribasketball.com</strong></li>
                            <li>Amount: <strong>${registration.Season.Price}</strong></li>
                            <li>Message: <strong>Season Registration - {registration.User.FirstName} {registration.User.LastName}</strong></li>
                        </ul>
                        <p style='color: #444; margin: 5px 0;'>Your spot will be secured once payment is received.</p>
                    </div>

                    <p style='color: #666; font-size: 14px;'>If you have any questions about payment or registration, please contact us.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                </div>
            </body>
            </html>";
            await SendEmailAsync(registration.User.Email, subject, htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send season registration confirmation email for user {UserId}",
                registration.UserId);
            throw;
        }
    }

    public async Task SendSeasonRegistrationCancelledEmailAsync(string userEmail, Season season)
    {
        try
        {
            _logger.LogInformation("Preparing season registration cancellation email for {Email}", userEmail);

            var subject = "Season Registration Cancelled - Saint Henri Basketball";
            var htmlContent = $@"
                <html>
                <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                    <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                        <h1 style='color: #333; text-align: center;'>Season Registration Cancelled</h1>
                        <p style='color: #666; font-size: 16px;'>Your registration for the following season has been cancelled:</p>
                        <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 5px 0; color: #444;'><strong>Season Period:</strong> {season.StartDate:MMM dd, yyyy} - {season.EndDate:MMM dd, yyyy}</p>
                        </div>
                        <p style='color: #666; font-size: 14px;'>If you believe this was done in error, please contact us.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                        <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(userEmail, subject, htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send season registration cancellation email to {Email}", userEmail);
            throw;
        }
    }

    public async Task SendSeasonStatusUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers)
    {
        try
        {
            var subject = $"Season Status Update - Saint Henri Basketball";
            var statusMessage = season.Status == SeasonStatus.Open ? "opened" : "closed";

            foreach (var user in registeredUsers)
            {
                var htmlContent = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                        <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                            <h1 style='color: #333; text-align: center;'>Season Status Update</h1>
                            <p style='color: #666; font-size: 16px;'>The basketball season has been {statusMessage}:</p>
                            <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                <p style='margin: 5px 0; color: #444;'><strong>Season Period:</strong> {season.StartDate:MMM dd, yyyy} - {season.EndDate:MMM dd, yyyy}</p>
                                <p style='margin: 5px 0; color: #444;'><strong>Status:</strong> {season.Status}</p>
                                <p style='margin: 5px 0; color: #444;'><strong>Price:</strong> ${season.Price}</p>
                            </div>
                            {(season.Status == SeasonStatus.Open ?
                                "<p style='color: #666; font-size: 14px;'>You can now register for sessions.</p>" :
                                "<p style='color: #666; font-size: 14px;'>Registration for new sessions is now closed.</p>")}
                            <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                            <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                        </div>
                    </body>
                    </html>";

                await SendEmailAsync(user.Email, subject, htmlContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send season status update emails");
            throw;
        }
    }

    public async Task SendSeasonUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers,
        string[] changedProperties)
    {
        try
        {
            var subject = "Season Update - Saint Henri Basketball";
            var changes = string.Join(", ", changedProperties.Select(p => p.ToLower()));

            foreach (var user in registeredUsers)
            {
                var htmlContent = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                        <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                            <h1 style='color: #333; text-align: center;'>Season Update</h1>
                            <p style='color: #666; font-size: 16px;'>The following details have been updated: {changes}</p>
                            <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                <p style='margin: 5px 0; color: #444;'><strong>Season Period:</strong> {season.StartDate:MMM dd, yyyy} - {season.EndDate:MMM dd, yyyy}</p>
                                <p style='margin: 5px 0; color: #444;'><strong>Price:</strong> ${season.Price}</p>
                                {(!string.IsNullOrEmpty(season.Notes) ?
                                    $"<p style='margin: 5px 0; color: #444;'><strong>Notes:</strong> {season.Notes}</p>" : "")}
                            </div>
                            <p style='color: #666; font-size: 14px;'>If you have any questions about these changes, please contact us.</p>
                            <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                            <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                        </div>
                    </body>
                    </html>";

                await SendEmailAsync(user.Email, subject, htmlContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send season update emails");
            throw;
        }
    }

    public async Task SendSeasonPaymentReminderEmailAsync(SeasonRegistration registration)
    {
        try
        {
            _logger.LogInformation("Preparing season payment reminder email for user {UserId}",
                registration.UserId);

            var subject = "Payment Reminder - Saint Henri Basketball Season";
            var htmlContent = $@"
                <html>
                <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                    <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                        <h1 style='color: #333; text-align: center;'>Payment Reminder</h1>
                        <p style='color: #666; font-size: 16px;'>This is a friendly reminder about your season registration payment:</p>
                        <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 5px 0; color: #444;'><strong>Season Period:</strong> {registration.Season.StartDate:MMM dd, yyyy} - {registration.Season.EndDate:MMM dd, yyyy}</p>
                            <p style='margin: 5px 0; color: #444;'><strong>Amount Due:</strong> ${registration.Season.Price}</p>
                            <p style='margin: 5px 0; color: #444;'><strong>Registration Date:</strong> {registration.RegisteredOn:MMM dd, yyyy}</p>
                        </div>
                        <p style='color: #666; font-size: 14px;'>Please complete your payment to secure your spot in the season.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                        <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                    </div>
                </body>
                </html>";

            await SendEmailAsync(registration.User.Email, subject, htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send season payment reminder email for user {UserId}",
                registration.UserId);
            throw;
        }
    }

    public async Task SendPaymentPlanUpdateEmailAsync(string userEmail, string userName, PaymentPlan paymentPlan)
    {
        try
        {
            _logger.LogInformation("Preparing payment plan update email for user {Email}", userEmail);

            var subject = "Payment Plan Updated - Saint Henri Basketball";
            var htmlContent = $@"
           <html>
           <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
               <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                   <h1 style='color: #333; text-align: center;'>Payment Plan Update</h1>
                   <p style='color: #666; font-size: 16px;'>Hi {userName}, your payment plan has been updated.</p>
                   
                   <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                       <p style='margin: 5px 0; color: #444;'><strong>New Payment Plan:</strong> {paymentPlan}</p>
                       <p style='margin: 5px 0; color: #444;'><strong>Updated On:</strong> {DateTime.UtcNow:MMM dd, yyyy}</p>
                   </div>

                   <div style='background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                       <p style='color: #444; margin: 5px 0;'><strong>Payment Instructions:</strong></p>
                       <ul style='color: #444; margin: 10px 0;'>
                           <li>Send payment via Interac e-Transfer to: <strong>pay@sainthenribasketball.com</strong></li>
                           <li>Include your full name and 'Payment Plan Update' in the message</li>
                       </ul>
                   </div>

                   <p style='color: #666; font-size: 14px;'>If you have any questions about your payment plan or need assistance, please don't hesitate to contact us.</p>
                   <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                   <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
               </div>
           </body>
           </html>";

            await SendEmailAsync(userEmail, subject, htmlContent);

            // Also notify admin about the payment plan change
            await SendPaymentPlanChangeNotificationToAdminAsync(userEmail, userName, paymentPlan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment plan update email to user {Email}", userEmail);
            throw;
        }
    }

    public async Task SendNewUserNotificationToAdminAsync(ApplicationUser newUser)
    {
        try
        {
            _logger.LogInformation("Sending new user notification email to admin for user {Email}", newUser.Email);

            var subject = "New User Registration - Saint Henri Basketball";
            var htmlContent = $@"
            <html>
            <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                    <h1 style='color: #333; text-align: center;'>New User Registration</h1>
                    <p style='color: #666; font-size: 16px;'>A new user has registered for Saint Henri Basketball.</p>
                    
                    <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p style='margin: 5px 0; color: #444;'><strong>Name:</strong> {newUser.FirstName} {newUser.LastName}</p>
                        <p style='margin: 5px 0; color: #444;'><strong>Email:</strong> {newUser.Email}</p>
                        <p style='margin: 5px 0; color: #444;'><strong>Username:</strong> {newUser.Username}</p>
                        <p style='margin: 5px 0; color: #444;'><strong>Payment Plan:</strong> {newUser.PaymentPlan}</p>
                        <p style='margin: 5px 0; color: #444;'><strong>Registration Date:</strong> {DateTime.UtcNow:MMM dd, yyyy HH:mm} UTC</p>
                    </div>

                    <p style='color: #666; font-size: 14px;'>You can manage users from the admin dashboard.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                </div>
            </body>
            </html>";

            await SendEmailAsync("admin@sainthenribasketball.com", subject, htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin notification email for new user {Email}", newUser.Email);
            // Don't throw - this is a notification email, shouldn't block registration
        }
    }
    
    private async Task SendPaymentPlanChangeNotificationToAdminAsync(string userEmail, string userName, PaymentPlan paymentPlan)
    {
        try
        {
            var subject = "Payment Plan Change Notification - Saint Henri Basketball";
            var htmlContent = $@"
           <html>
           <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
               <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                   <h1 style='color: #333; text-align: center;'>Payment Plan Change</h1>
                   <p style='color: #666; font-size: 16px;'>A user has updated their payment plan.</p>
                   
                   <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                       <p style='margin: 5px 0; color: #444;'><strong>User:</strong> {userName}</p>
                       <p style='margin: 5px 0; color: #444;'><strong>Email:</strong> {userEmail}</p>
                       <p style='margin: 5px 0; color: #444;'><strong>New Payment Plan:</strong> {paymentPlan}</p>
                       <p style='margin: 5px 0; color: #444;'><strong>Changed On:</strong> {DateTime.UtcNow:MMM dd, yyyy HH:mm} UTC</p>
                   </div>

                   <p style='color: #666; font-size: 14px;'>You can view more details in the admin dashboard.</p>
                   <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                   <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
               </div>
           </body>
           </html>";

            await SendEmailAsync("admin@sainthenribasketball.com", subject, htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send admin notification for payment plan change. User: {Email}", userEmail);
        }
    }

    public async Task SendAttendanceReminderEmailAsync(string userEmail, string userName, string? customMessage = null)
    {
        try
        {
            _logger.LogInformation("Preparing attendance reminder email for user {Email}", userEmail);

            var subject = "Session Attendance Reminder - Saint Henri Basketball";
            var htmlContent = $@"
            <html>
            <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                    <h1 style='color: #333; text-align: center;'>Session Attendance Reminder</h1>
                    <p style='color: #666; font-size: 16px;'>Hi {userName},</p>

                    {(!string.IsNullOrEmpty(customMessage) ? $@"
                    <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p style='margin: 5px 0; color: #444;'>{customMessage}</p>
                    </div>" : "")}

                    <p style='color: #666; font-size: 14px;'>If you cannot attend, please let us know in advance.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                </div>
            </body>
            </html>";

            await SendEmailAsync(userEmail, subject, htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send attendance reminder email to user {Email}", userEmail);
            throw;
        }
    }

    public async Task SendSeasonRegistrationReminderEmailAsync(string userEmail, string userName, string? customMessage = null)
    {
        try
        {
            _logger.LogInformation("Preparing season registration reminder email for user {Email}", userEmail);

            var subject = "Season Registration Reminder - Saint Henri Basketball";
            var htmlContent = $@"
            <html>
            <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                    <h1 style='color: #333; text-align: center;'>Season Registration Reminder</h1>
                    <p style='color: #666; font-size: 16px;'>Hi {userName},</p>

                    {(!string.IsNullOrEmpty(customMessage) ? $@"
                    <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p style='margin: 5px 0; color: #444;'>{customMessage}</p>
                    </div>" : "")}

                    <div style='background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p style='color: #444; margin: 5px 0;'><strong>Payment Instructions:</strong></p>
                        <ul style='color: #444; margin: 10px 0;'>
                            <li>Send payment via Interac e-Transfer to: <strong>pay@sainthenribasketball.com</strong></li>
                            <li>Include your full name in the message</li>
                        </ul>
                    </div>

                    <p style='color: #666; font-size: 14px;'>If you have any questions, please don't hesitate to contact us.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                </div>
            </body>
            </html>";

            await SendEmailAsync(userEmail, subject, htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send season registration reminder email to user {Email}", userEmail);
            throw;
        }
    }

    public async Task SendGeneralAnnouncementEmailAsync(string userEmail, string userName, string message)
    {
        try
        {
            _logger.LogInformation("Preparing general announcement email for user {Email}", userEmail);

            var subject = "Announcement - Saint Henri Basketball";
            var htmlContent = $@"
            <html>
            <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                    <h1 style='color: #333; text-align: center;'>Important Announcement</h1>
                    <p style='color: #666; font-size: 16px;'>Hi {userName},</p>

                    <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p style='margin: 5px 0; color: #444;'>{message}</p>
                    </div>

                    <p style='color: #666; font-size: 14px;'>If you have any questions, please don't hesitate to contact us.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                </div>
            </body>
            </html>";

            await SendEmailAsync(userEmail, subject, htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send general announcement email to user {Email}", userEmail);
            throw;
        }
    }

    public async Task SendPaymentReminderEmailAsync(string userEmail, string userName, PaymentPlan paymentPlan, string? customMessage = null)
    {
        try
        {
            _logger.LogInformation("Preparing payment reminder email for user {Email}", userEmail);

            var subject = "Payment Reminder - Saint Henri Basketball";
            var htmlContent = $@"
            <html>
            <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
                <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                    <h1 style='color: #333; text-align: center;'>Payment Reminder</h1>
                    <p style='color: #666; font-size: 16px;'>Hi {userName},</p>
                    
                    {(!string.IsNullOrEmpty(customMessage) ? $@"
                    <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p style='margin: 5px 0; color: #444;'>{customMessage}</p>
                    </div>" : "")}

                    <div style='background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                        <p style='color: #444; margin: 5px 0;'><strong>Payment Plan:</strong> {paymentPlan}</p>
                        <p style='color: #444; margin: 15px 0;'><strong>Payment Instructions:</strong></p>
                        <ul style='color: #444; margin: 10px 0;'>
                            <li>Send payment via Interac e-Transfer to: <strong>pay@sainthenribasketball.com</strong></li>
                            <li>Include your full name in the message</li>
                        </ul>
                    </div>

                    <p style='color: #666; font-size: 14px;'>If you've already made your payment, please disregard this reminder. If you have any questions, please don't hesitate to contact us.</p>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
                </div>
            </body>
            </html>";

            await SendEmailAsync(userEmail, subject, htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment reminder email to user {Email}", userEmail);
            throw;
        }
    }

    private Task<string> BuildEmailContentAsync(EmailType emailType, ApplicationUser user, EmailLanguage language, string? customMessage, string? customMessageFr)
    {
        var userName = $"{user.FirstName} {user.LastName}";

        return Task.FromResult(language switch
        {
            EmailLanguage.English => BuildEnglishContent(emailType, userName, user.PaymentPlan, customMessage),
            EmailLanguage.French => BuildFrenchContent(emailType, userName, user.PaymentPlan, customMessageFr ?? customMessage),
            EmailLanguage.Bilingual => BuildBilingualContent(emailType, userName, user.PaymentPlan, customMessage, customMessageFr),
            _ => BuildEnglishContent(emailType, userName, user.PaymentPlan, customMessage)
        });
    }

    private string BuildEnglishContent(EmailType emailType, string userName, PaymentPlan paymentPlan, string? customMessage)
    {
        var messageContent = customMessage ?? GetDefaultEnglishMessage(emailType, userName, paymentPlan);

        return $@"
        <html>
        <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
            <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                <h1 style='color: #333; text-align: center;'>{GetEmailTitle(emailType, EmailLanguage.English)}</h1>
                <p style='color: #666; font-size: 16px;'>Hi {userName},</p>
                <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    <p style='margin: 5px 0; color: #444;'>{messageContent}</p>
                </div>
                {GetPaymentInstructions(EmailLanguage.English)}
                <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
            </div>
        </body>
        </html>";
    }

    private string BuildFrenchContent(EmailType emailType, string userName, PaymentPlan paymentPlan, string? customMessage)
    {
        var messageContent = customMessage ?? GetDefaultFrenchMessage(emailType, userName, paymentPlan);

        return $@"
        <html>
        <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
            <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                <h1 style='color: #333; text-align: center;'>{GetEmailTitle(emailType, EmailLanguage.French)}</h1>
                <p style='color: #666; font-size: 16px;'>Bonjour {userName},</p>
                <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    <p style='margin: 5px 0; color: #444;'>{messageContent}</p>
                </div>
                {GetPaymentInstructions(EmailLanguage.French)}
                <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
            </div>
        </body>
        </html>";
    }

    private string BuildBilingualContent(EmailType emailType, string userName, PaymentPlan paymentPlan, string? customMessageEn, string? customMessageFr)
    {
        var englishContent = customMessageEn ?? GetDefaultEnglishMessage(emailType, userName, paymentPlan);
        var frenchContent = customMessageFr ?? GetDefaultFrenchMessage(emailType, userName, paymentPlan);

        return $@"
        <html>
        <body style='font-family: Arial, sans-serif; margin: 0; padding: 20px;'>
            <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 20px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.1);'>
                <h1 style='color: #333; text-align: center;'>{GetEmailTitle(emailType, EmailLanguage.English)} / {GetEmailTitle(emailType, EmailLanguage.French)}</h1>
                <p style='color: #666; font-size: 16px;'>Hi/Bonjour {userName},</p>
                
                <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    <h2 style='color: #333; margin-top: 0;'>English</h2>
                    <p style='margin: 5px 0; color: #444;'>{englishContent}</p>
                </div>

                <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    <h2 style='color: #333; margin-top: 0;'>Français</h2>
                    <p style='margin: 5px 0; color: #444;'>{frenchContent}</p>
                </div>

                {GetPaymentInstructions(EmailLanguage.Bilingual)}
                <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
                <p style='color: #999; font-size: 12px; text-align: center;'>Saint Henri Basketball</p>
            </div>
        </body>
        </html>";
    }

    private object GetEmailTitle(EmailType emailType, EmailLanguage language)
    {
        return emailType switch
        {
            EmailType.PaymentReminder => language switch
            {
                EmailLanguage.English => "Payment Reminder",
                EmailLanguage.French => "Rappel de paiement",
                EmailLanguage.Bilingual => "Payment Reminder / Rappel de paiement",
                _ => "Saint Henri Basketball"
            },
            EmailType.AttendanceReminder => language switch
            {
                EmailLanguage.English => "Attendance Reminder",
                EmailLanguage.French => "Rappel de présence",
                EmailLanguage.Bilingual => "Attendance Reminder / Rappel de présence",
                _ => "Saint Henri Basketball"
            },
            EmailType.SeasonRegistrationReminder => language switch
            {
                EmailLanguage.English => "Season Registration Reminder",
                EmailLanguage.French => "Rappel d'inscription à la saison",
                EmailLanguage.Bilingual => "Season Registration Reminder / Rappel d'inscription",
                _ => "Saint Henri Basketball"
            },
            EmailType.GeneralAnnouncement => language switch
            {
                EmailLanguage.English => "Announcement",
                EmailLanguage.French => "Annonce",
                EmailLanguage.Bilingual => "Announcement / Annonce",
                _ => "Saint Henri Basketball"
            },
            _ => "Saint Henri Basketball"
        };
    }

    private  string GetDefaultEnglishMessage(EmailType emailType, string userName, PaymentPlan paymentPlan) => emailType switch
    {
        EmailType.PaymentReminder => EmailMessagesEn.Payment.PaymentDue(GetPaymentAmount(paymentPlan)),
        EmailType.AttendanceReminder => EmailMessagesEn.Attendance.ReminderMessage(DateTime.Now.ToShortDateString(), "10:00 AM"),
        EmailType.SeasonRegistrationReminder => EmailMessagesEn.Season.RegistrationReminder(DateTime.Now.AddDays(14).ToShortDateString(), GetSeasonPrice()),
        EmailType.FacilityUpdate => EmailMessagesEn.General.FacilityUpdate,
        EmailType.ScheduleChange => EmailMessagesEn.General.ScheduleChange,
        EmailType.LowAttendanceWarning => EmailMessagesEn.Attendance.LowAttendanceWarning,
        EmailType.GeneralAnnouncement => EmailMessagesEn.General.WelcomeMessage(userName.Split(' ')[0]),
        _ => string.Empty
    };

    private string GetDefaultFrenchMessage(EmailType emailType, string userName, PaymentPlan paymentPlan) => emailType switch
    {
        EmailType.PaymentReminder => EmailMessagesFr.Payment.PaymentDue(GetPaymentAmount(paymentPlan)),
        EmailType.AttendanceReminder => EmailMessagesFr.Attendance.ReminderMessage(DateTime.Now.AddDays(8).ToShortDateString(), "10h00"),
        EmailType.SeasonRegistrationReminder => EmailMessagesFr.Season.RegistrationReminder(DateTime.Now.AddDays(8).ToShortDateString(), GetSeasonPrice()),
        EmailType.FacilityUpdate => EmailMessagesFr.General.FacilityUpdate,
        EmailType.ScheduleChange => EmailMessagesFr.General.ScheduleChange,
        EmailType.LowAttendanceWarning => EmailMessagesFr.Attendance.LowAttendanceWarning,
        EmailType.GeneralAnnouncement => EmailMessagesFr.General.WelcomeMessage(userName.Split(' ')[0]),
        _ => string.Empty
    };

    private string GetPaymentInstructions(EmailLanguage language)
    {
        if (language == EmailLanguage.English)
        {
            return @"
            <div style='background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                <p style='color: #444; margin: 5px 0;'><strong>Payment Instructions:</strong></p>
                <p style='color: #444; margin: 5px 0;'>Send payment via Interac e-Transfer to: <strong>pay@sainthenribasketball.com</strong></p>
            </div>";
        }

        if (language == EmailLanguage.French)
        {
            return @"
            <div style='background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                <p style='color: #444; margin: 5px 0;'><strong>Instructions de paiement :</strong></p>
                <p style='color: #444; margin: 5px 0;'>Envoyez le paiement par virement Interac à : <strong>pay@sainthenribasketball.com</strong></p>
            </div>";
        }

        // Bilingual
        return @"
        <div style='background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
            <p style='color: #444; margin: 5px 0;'><strong>Payment Instructions / Instructions de paiement :</strong></p>
            <p style='color: #444; margin: 5px 0;'>Send payment via Interac e-Transfer to: <strong>pay@sainthenribasketball.com</strong></p>
            <p style='color: #444; margin: 5px 0;'>Envoyez le paiement par virement Interac à : <strong>pay@sainthenribasketball.com</strong></p>
        </div>";
    }

    private string GetEmailSubject(EmailType emailType, EmailLanguage language)
    {
        return (emailType, language) switch
        {
            (EmailType.PaymentReminder, EmailLanguage.French) => "Rappel de paiement - Saint Henri Basketball",
            (EmailType.PaymentReminder, EmailLanguage.English) => "Payment Reminder - Saint Henri Basketball",
            (EmailType.PaymentReminder, EmailLanguage.Bilingual) => "Payment Reminder / Rappel de paiement - Saint Henri Basketball",

            (EmailType.AttendanceConfirmation, EmailLanguage.French) => "Rappel de présence - Saint Henri Basketball",
            (EmailType.AttendanceConfirmation, EmailLanguage.English) => "Attendance Reminder - Saint Henri Basketball",
            (EmailType.AttendanceConfirmation, EmailLanguage.Bilingual) => "Attendance Reminder / Rappel de présence - Saint Henri Basketball",

            (EmailType.SeasonRegistrationReminder, EmailLanguage.French) => "Rappel d'inscription à la saison - Saint Henri Basketball",
            (EmailType.SeasonRegistrationReminder, EmailLanguage.English) => "Season Registration Reminder - Saint Henri Basketball",
            (EmailType.SeasonRegistrationReminder, EmailLanguage.Bilingual) => "Season Registration Reminder / Rappel d'inscription - Saint Henri Basketball",

            (EmailType.GeneralAnnouncement, EmailLanguage.French) => "Annonce - Saint Henri Basketball",
            (EmailType.GeneralAnnouncement, EmailLanguage.English) => "Announcement - Saint Henri Basketball",
            (EmailType.GeneralAnnouncement, EmailLanguage.Bilingual) => "Announcement / Annonce - Saint Henri Basketball",

            _ => "Saint Henri Basketball"
        };
    }
    private decimal GetPaymentAmount(PaymentPlan plan) => plan switch
    {
        PaymentPlan.Season => 100.00m,
        PaymentPlan.DropIn => 10.00m,
        _ => 0.00m
    };

    private decimal GetSeasonPrice() => 100.00m;

    public async Task<EmailSendResult> SendTargetedEmailsAsync(EmailType emailType, List<string> emails, EmailLanguage language, string? customMessage, string? customMessageFr)
    {
        var result = new EmailSendResult();

        foreach (var email in emails)
        {
            try
            {
                if (!new EmailAddressAttribute().IsValid(email))
                {
                    result.FailureCount++;
                    result.FailedEmails.Add(email);
                    _logger.LogWarning("Invalid email format: {Email}", email);
                    continue;
                }

                // Get user details by email
                var user = await _userRepository.GetByEmailAsync(email);
                if (user == null)
                {
                    result.FailureCount++;
                    result.FailedEmails.Add(email);
                    _logger.LogWarning("User not found for email: {Email}", email);
                    continue;
                }

                var subject = GetEmailSubject(emailType, language);
                var htmlContent = await BuildEmailContentAsync(emailType, user, language, customMessage, customMessageFr);

                var apiKey = _configuration["SendGrid:ApiKey"];
                var fromEmail = _configuration["SendGrid:FromEmail"];
                var fromName = _configuration["SendGrid:FromName"];

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(fromEmail, fromName);
                var toAddress = new EmailAddress(email, $"{user.FirstName} {user.LastName}");
                var msg = MailHelper.CreateSingleEmail(from, toAddress, subject, null, htmlContent);

                var response = await client.SendEmailAsync(msg);
                var responseBody = await response.Body.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    result.FailureCount++;
                    result.FailedEmails.Add(email);
                    _logger.LogError("Failed to send email. Status Code: {StatusCode}, Body: {Body}",
                        response.StatusCode, responseBody);
                    continue;
                }

                result.SuccessCount++;
                _logger.LogInformation("Successfully sent {EmailType} email in {Language} to {Email}",
                    emailType, language, email);
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.FailedEmails.Add(email);
                _logger.LogError(ex, "Failed to send {EmailType} email in {Language} to {Email}",
                    emailType, language, email);
            }
        }

        return result;
    }

    public async Task<EmailSendResult> SendPaymentRemindersAsync(List<string> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null)
    {
        return await SendEmailsAsync(
            EmailType.PaymentReminder,
            emails,
            language,
            customMessage,
            customMessageFr);
    }

    public async Task<EmailSendResult> SendAttendanceRemindersAsync(List<string> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null)
    {
        return await SendEmailsAsync(
            EmailType.AttendanceReminder,
            emails,
            language,
            customMessage,
            customMessageFr);
    }

    public async Task<EmailSendResult> SendSeasonRegistrationRemindersAsync(List<string> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null)
    {
        return await SendEmailsAsync(
            EmailType.SeasonRegistrationReminder,
            emails,
            language,
            customMessage,
            customMessageFr);
    }

    public async Task<EmailSendResult> SendFacilityUpdatesAsync(List<string> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null)
    {
        return await SendEmailsAsync(
            EmailType.FacilityUpdate,
            emails,
            language,
            customMessage,
            customMessageFr);
    }
    public async Task<EmailSendResult> SendScheduleChangesAsync(List<string> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null)
    {
        return await SendEmailsAsync(
            EmailType.ScheduleChange,
            emails,
            language,
            customMessage,
            customMessageFr);
    }

    public async Task<EmailSendResult> SendGeneralAnnouncementsAsync(List<string> emails, EmailLanguage language, string? customMessage = null, string? customMessageFr = null)
    {
        return await SendEmailsAsync(
            EmailType.GeneralAnnouncement,
            emails,
            language,
            customMessage,
            customMessageFr);
    }

    private async Task<EmailSendResult> SendEmailsAsync(
    EmailType emailType,
    List<string> emails,
    EmailLanguage language,
    string? customMessage,
    string? customMessageFr)
    {
        var result = new EmailSendResult();

        foreach (var email in emails)
        {
            try
            {
                if (!new EmailAddressAttribute().IsValid(email))
                {
                    result.FailureCount++;
                    result.FailedEmails.Add(email);
                    _logger.LogWarning("Invalid email format: {Email}", email);
                    continue;
                }

                var user = await _userRepository.GetByEmailAsync(email);
                if (user == null)
                {
                    result.FailureCount++;
                    result.FailedEmails.Add(email);
                    _logger.LogWarning("User not found for email: {Email}", email);
                    continue;
                }

                var subject = GetEmailSubject(emailType, language);
                
                var htmlContent = await BuildEmailContentAsync(emailType, user, language, customMessage, customMessageFr);

                // SendGrid configuration
                var apiKey = _configuration["SendGrid:ApiKey"];
                var fromEmail = _configuration["SendGrid:FromEmail"];
                var fromName = _configuration["SendGrid:FromName"];

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(fromEmail, fromName);
                var to = new EmailAddress(email, $"{user.FirstName} {user.LastName}");
                var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);

                var response = await client.SendEmailAsync(msg);
                var responseBody = await response.Body.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    result.FailureCount++;
                    result.FailedEmails.Add(email);
                    _logger.LogError("Failed to send email. Status Code: {StatusCode}, Body: {Body}",
                        response.StatusCode, responseBody);
                    continue;
                }

                result.SuccessCount++;
                _logger.LogInformation("Successfully sent {EmailType} email in {Language} to {Email}",
                    emailType, language, email);
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.FailedEmails.Add(email);
                _logger.LogError(ex, "Failed to send {EmailType} email in {Language} to {Email}",
                    emailType, language, email);
            }
        }

        return result;
    }
}