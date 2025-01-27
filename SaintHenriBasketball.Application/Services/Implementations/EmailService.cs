using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly SendGridClient _client;
    private readonly EmailAddress _fromAddress;
    private readonly ILogger<EmailService> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private const int MaxRetries = 3;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger,
        IUserRepository userRepository,
        IBackgroundJobClient backgroundJobs,
        IWebHostEnvironment webHostEnvironment)
    {
        _logger = logger;
        _userRepository = userRepository;
        _backgroundJobs = backgroundJobs;
        _webHostEnvironment = webHostEnvironment;

        var apiKey = configuration["SendGrid:ApiKey"];
        var fromEmail = configuration["SendGrid:FromEmail"];
        var fromName = configuration["SendGrid:FromName"];

        _client = new SendGridClient(apiKey);
        _fromAddress = new EmailAddress(fromEmail, fromName);
    }

    #region Base Email Methods

    public async Task SendEmailAsync(string? to, string subject, string htmlContent)
    {
        var toAddress = new EmailAddress(to);
        var msg = MailHelper.CreateSingleEmail(_fromAddress, toAddress, subject, null, htmlContent);
        await SendWithRetryAsync(msg);
    }

    public async Task SendWithRetryAsync(SendGridMessage msg, int retryCount = 0)
    {
        try
        {
            var response = await _client.SendEmailAsync(msg);
            
            if (!response.IsSuccessStatusCode && retryCount < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                _backgroundJobs.Schedule(() => SendWithRetryAsync(msg, retryCount + 1), delay);
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Body.ReadAsStringAsync();
                throw new Exception($"Failed to send email after {MaxRetries} retries. Status: {response.StatusCode}, Body: {responseBody}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email. Retry count: {RetryCount}", retryCount);
            throw;
        }
    }
    #endregion

    #region Authentication Emails

    public async Task SendConfirmationEmailAsync(string? to, string confirmationLink)
    {
        var content = EmailTemplateHelper.BuildEmailLayout(
            "Welcome to Saint Henri Basketball!",
            $@"<p style='{EmailTemplateHelper.Styles.Content}'>Please confirm your email address:</p>
                {EmailTemplateHelper.BuildButton("Confirm Email", confirmationLink)}
                <p style='{EmailTemplateHelper.Styles.Content}'>Or copy and paste this link:</p>
                <p style='{EmailTemplateHelper.Styles.Content}'>{confirmationLink}</p>"
        );

        await SendEmailAsync(to, "Confirm your email - Saint Henri Basketball", content);
    }

    public async Task SendPasswordResetEmailAsync(string? to, string resetLink)
    {
        var content = EmailTemplateHelper.BuildEmailLayout(
            "Reset Your Password",
            $@"<p style='{EmailTemplateHelper.Styles.Content}'>You requested to reset your password:</p>
                {EmailTemplateHelper.BuildButton("Reset Password", resetLink)}
                <p style='{EmailTemplateHelper.Styles.Content}'>This link will expire in 1 hour.</p>
                <p style='{EmailTemplateHelper.Styles.Content}'>If you didn't request this, please ignore this email.</p>"
        );

        await SendEmailAsync(to, "Reset Your Password - Saint Henri Basketball", content);
    }

    #endregion

    #region Payment Emails

public async Task SendPaymentCreatedConfirmationAsync(string? email, decimal amount, string? reference, EmailLanguage language = EmailLanguage.French)
{
    try
    {
        var user = await _userRepository.GetByEmailAsync(email)
            ?? throw new ArgumentException($"User not found for email: {email}");

        var userName = $"{user.FirstName} {user.LastName}";
        var billLanguage = language == EmailLanguage.French ? "fr" : "en";
        var culture = billLanguage == "fr" ? new CultureInfo("fr-CA") : new CultureInfo("en-CA");
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("America/Montreal"));

        var paymentInstructions = new Dictionary<string, string?>
        {
            { language == EmailLanguage.French ? "Envoyer à" : "Send to", "pay@sainthenribasketball.com" },
            { language == EmailLanguage.French ? "Montant dû" : "Amount Due", $"${amount}" },
            { language == EmailLanguage.French ? "Message" : "Message", $"{user.FirstName} {user.LastName} - {reference}" }
        };

        var emailContent = EmailTemplateHelper.BuildEmailLayout(
            language == EmailLanguage.French ? "Rappel de paiement" : "Payment Reminder",
            $@"<p style='{EmailTemplateHelper.Styles.Content}'>{(language == EmailLanguage.French ? "Bonjour" : "Hi")} {userName},</p>
                <div style='background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    <h2 style='color: #2e7d32; margin-top: 0;'>{(language == EmailLanguage.French ? "Instructions de paiement" : "Payment Instructions")}</h2>
                    {EmailTemplateHelper.BuildInfoBox(paymentInstructions)}
                    <p style='{EmailTemplateHelper.Styles.Content}'>
                        {(language == EmailLanguage.French
                            ? "Veuillez effectuer votre paiement dès que possible. Vous trouverez la facture en pièce jointe."
                            : "Please complete your payment as soon as possible. You will find the bill attached.")}
                    </p>
                </div>"
        );

        var msg = new SendGridMessage();
        msg.SetFrom(_fromAddress);
        msg.AddTo(new EmailAddress(email));
        msg.SetSubject(language == EmailLanguage.French
            ? "Rappel de paiement - Saint Henri Basketball"
            : "Payment Reminder - Saint Henri Basketball");
        msg.AddContent(MimeType.Html, emailContent);

        try
        {
            var billGenerator = new BillPdfGenerator(_webHostEnvironment);
            var description = language == EmailLanguage.French
                ? user.PaymentPlan == PaymentPlan.Season
                    ? "Forfait de saison"
                    : "Forfait à la séance"
                : user.PaymentPlan == PaymentPlan.Season
                    ? "Season Plan"
                    : "Drop-in Plan";

            var billDetails = new BillDetails
            {
                Name = userName,
                Email = email,
                Description = description,
                Amount = amount,
                Reference = reference,
                Date = localTime
            };

            var pdfContent = billGenerator.GenerateBill(billDetails, billLanguage);

            msg.AddAttachment(
                language == EmailLanguage.French ? "facture.pdf" : "bill.pdf",
                Convert.ToBase64String(pdfContent),
                "application/pdf",
                "attachment"
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate PDF bill. Sending email without attachment.");
        }

        await SendWithRetryAsync(msg);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to send payment reminder email to {Email}", email);
        throw;
    }
}
    public async Task SendPaymentConfirmationAsync(string? email, decimal amount, string? reference,
        EmailLanguage language = EmailLanguage.French)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(email)
                       ?? throw new ArgumentException($"User not found for email: {email}");

            var userName = $"{user.FirstName} {user.LastName}";
            var billLanguage = language == EmailLanguage.French ? "fr" : "en";
            var culture = billLanguage == "fr" ? new CultureInfo("fr-CA") : new CultureInfo("en-CA");
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("America/Montreal"));

            var fields = new Dictionary<string, string?>
            {
                { language == EmailLanguage.French ? "Référence" : "Payment Reference", reference },
                { language == EmailLanguage.French ? "Montant payé" : "Amount Paid", $"${amount}" },
                { "Date", localTime.ToString("MMM dd, yyyy HH:mm", culture) }
            };

            var emailContent = EmailTemplateHelper.BuildEmailLayout(
                language == EmailLanguage.French ? "Confirmation de paiement" : "Payment Confirmation",
                $@"<p style='{EmailTemplateHelper.Styles.Content}'>{(language == EmailLanguage.French ? "Bonjour" : "Hi")} {userName},</p>
                    {EmailTemplateHelper.BuildInfoBox(fields)}
                    <p style='{EmailTemplateHelper.Styles.Content}'>
                        {(language == EmailLanguage.French
                            ? "Merci pour votre paiement. Veuillez trouver votre facture en pièce jointe."
                            : "Thank you for your payment. Please find your bill attached.")}
                    </p>"
            );

            var msg = new SendGridMessage();
            msg.SetFrom(_fromAddress);
            msg.AddTo(new EmailAddress(email));
            msg.SetSubject(language == EmailLanguage.French
                ? "Confirmation de paiement - Saint Henri Basketball"
                : "Payment Confirmation - Saint Henri Basketball");
            msg.AddContent(MimeType.Html, emailContent);

            try
            {
                var billGenerator = new BillPdfGenerator(_webHostEnvironment);
                var description = language == EmailLanguage.French
                    ? user.PaymentPlan == PaymentPlan.Season
                        ? "Forfait de saison"
                        : "Forfait à la séance"
                    : user.PaymentPlan == PaymentPlan.Season
                        ? "Season Plan"
                        : "Drop-in Plan";

                var billDetails = new BillDetails
                {
                    Name = userName,
                    Email = email,
                    Description = description,
                    Amount = amount,
                    Reference = reference
                };

                var pdfContent = billGenerator.GenerateBill(billDetails, billLanguage);

                msg.AddAttachment(
                    language == EmailLanguage.French ? $"facture de {description}.pdf" : $"bill of {description}.pdf",
                    Convert.ToBase64String(pdfContent),
                    "application/pdf",
                    "attachment"
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate PDF bill. Sending email without attachment.");
            }

            await SendWithRetryAsync(msg);
            await NotifyAdminOfPayment(email, userName, amount, reference, language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment confirmation email to {Email}", email);
            throw;
        }
    }

    private async Task NotifyAdminOfPayment(string? userEmail, string userName, decimal amount, string? reference,
        EmailLanguage language = EmailLanguage.French)
    {
        var billLanguage = language == EmailLanguage.French ? "fr" : "en";
        var culture = billLanguage == "fr" ? new CultureInfo("fr-CA") : new CultureInfo("en-CA");
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("America/Montreal"));

        var fields = new Dictionary<string, string?>
        {
            { "User", userName },
            { "Email", userEmail },
            { "Amount", $"${amount}" },
            { "Reference", reference },
            { "Date", localTime.ToString("MMM dd, yyyy HH:mm", culture) }
        };

        var content = EmailTemplateHelper.BuildEmailLayout(
            "Payment Received",
            EmailTemplateHelper.BuildInfoBox(fields)
        );

        await SendEmailAsync(
            "admin@sainthenribasketball.com",
            "Payment Received - Saint Henri Basketball",
            content
        );
    }

    #endregion

    #region Season Related Emails

    public async Task SendSeasonRegistrationConfirmationEmailAsync(SeasonRegistration registration)
    {
        var fields = new Dictionary<string, string?>
        {
            {
                "Season Period",
                $"{registration.Season.StartDate:MMM dd, yyyy} - {registration.Season.EndDate:MMM dd, yyyy}"
            },
            { "Registration Date", registration.RegisteredOn.ToString("MMM dd, yyyy") },
            { "Season Price", $"${registration.Season.Price}" }
        };

        var paymentInstructions = new Dictionary<string, string?>
        {
            { "Send to", "pay@sainthenribasketball.com" },
            { "Amount", $"${registration.Season.Price}" },
            { "Message", $"Season Registration - {registration.User.FirstName} {registration.User.LastName}" }
        };

        var content = EmailTemplateHelper.BuildEmailLayout(
            "Season Registration Confirmed!",
            $@"<p style='{EmailTemplateHelper.Styles.Content}'>Thank you for registering for the basketball season!</p>
                {EmailTemplateHelper.BuildInfoBox(fields)}
                <div style='background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    <h2 style='color: #2e7d32; margin-top: 0;'>Payment Instructions</h2>
                    {EmailTemplateHelper.BuildInfoBox(paymentInstructions)}
                    <p style='{EmailTemplateHelper.Styles.Content}'>Your spot will be secured once payment is received.</p>
                </div>"
        );

        await SendEmailAsync(
            registration.User.Email,
            "Season Registration Confirmation - Saint Henri Basketball",
            content
        );
    }

    public async Task SendAttendanceReminderEmailAsync(string? userEmail, string userName, string? customMessage = null)
    {
        var defaultMessage = $@"<div style='margin-top: 20px; text-align: center;'>
        <p style='{EmailTemplateHelper.Styles.Content}'>Please confirm your attendance by clicking below:</p>
        {EmailTemplateHelper.BuildButton("Confirm Attendance", "https://sainthenribasketball.com/attendance-confirmation")}
    </div>";

        var content = EmailTemplateHelper.BuildEmailLayout(
            "Attendance Reminder",
            customMessage ?? defaultMessage
        );

        await SendEmailAsync(userEmail, "Attendance Reminder - Saint Henri Basketball", content);
    }

    public async Task SendPaymentPlanUpdateEmailAsync(string? userEmail, string userName, PaymentPlan paymentPlan)
    {
        var fields = new Dictionary<string, string?>
        {
            { "New Payment Plan", paymentPlan.ToString() },
            { "Updated On", DateTime.UtcNow.ToString("MMM dd, yyyy") }
        };

        var content = EmailTemplateHelper.BuildEmailLayout(
            "Payment Plan Update",
            $@"<p style='{EmailTemplateHelper.Styles.Content}'>Hi {userName}, your payment plan has been updated.</p>
            {EmailTemplateHelper.BuildInfoBox(fields)}
            <div style='background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                <p style='{EmailTemplateHelper.Styles.Content}'><strong>Payment Instructions:</strong></p>
                <ul style='color: #444; margin: 10px 0;'>
                    <li>Send payment via Interac e-Transfer to: <strong>pay@sainthenribasketball.com</strong></li>
                    <li>Include your full name and 'Payment Plan Update' in the message</li>
                </ul>
            </div>"
        );

        await SendEmailAsync(userEmail, "Payment Plan Updated - Saint Henri Basketball", content);
    }

    public async Task SendAttendanceConfirmationEmailAsync(SessionAttendance attendance)
    {
        var fields = new Dictionary<string, string?>
        {
            { "Date", attendance.Session.SessionDate.ToString("dddd, MMMM dd, yyyy") },
            { "Time", $"{attendance.Session.StartTime:hh:mm tt} - {attendance.Session.EndTime:hh:mm tt}" },
            { "Location", attendance.Session.Location },
            { "Status", attendance.IsAttending ? "Present" : "Absent" }
        };

        if (!string.IsNullOrEmpty(attendance.Notes))
        {
            fields.Add("Notes", attendance.Notes);
        }

        var content = EmailTemplateHelper.BuildEmailLayout(
            "Attendance Confirmation",
            $@"<p style='{EmailTemplateHelper.Styles.Content}'>Your attendance has been recorded for the following session:</p>
            {EmailTemplateHelper.BuildInfoBox(fields)}"
        );

        await SendEmailAsync(
            attendance.User.Email,
            "Basketball Session Attendance Confirmation",
            content
        );
    }

    public async Task SendAttendanceUpdateEmailAsync(SessionAttendance attendance)
    {
        var fields = new Dictionary<string, string?>
        {
            { "Date", attendance.Session.SessionDate.ToString("dddd, MMMM dd, yyyy") },
            { "Time", $"{attendance.Session.StartTime:hh:mm tt} - {attendance.Session.EndTime:hh:mm tt}" },
            { "Location", attendance.Session.Location },
            { "Updated Status", attendance.IsAttending ? "Present" : "Absent" }
        };

        if (!string.IsNullOrEmpty(attendance.Notes))
        {
            fields.Add("Notes", attendance.Notes);
        }

        var content = EmailTemplateHelper.BuildEmailLayout(
            "Attendance Update",
            $@"<p style='{EmailTemplateHelper.Styles.Content}'>Your attendance status has been updated for the following session:</p>
            {EmailTemplateHelper.BuildInfoBox(fields)}"
        );

        await SendEmailAsync(
            attendance.User.Email,
            "Basketball Session Attendance Update",
            content
        );
    }

    public async Task SendSeasonRegistrationCancelledEmailAsync(string? userEmail, Season season)
    {
        var fields = new Dictionary<string, string?>
        {
            { "Season Period", $"{season.StartDate:MMM dd, yyyy} - {season.EndDate:MMM dd, yyyy}" }
        };

        var content = EmailTemplateHelper.BuildEmailLayout(
            "Season Registration Cancelled",
            $@"<p style='{EmailTemplateHelper.Styles.Content}'>Your registration for the following season has been cancelled:</p>
            {EmailTemplateHelper.BuildInfoBox(fields)}
            <p style='{EmailTemplateHelper.Styles.Content}'>If you believe this was done in error, please contact us.</p>"
        );

        await SendEmailAsync(userEmail, "Season Registration Cancelled - Saint Henri Basketball", content);
    }

    public async Task SendSeasonRegistrationReminderEmailAsync(string? userEmail, string userName,
        string? customMessage = null)
    {
        var fields = new Dictionary<string, string?>
        {
            { "Send to", "pay@sainthenribasketball.com" },
            { "Include", "Your full name in the message" }
        };

        var content = EmailTemplateHelper.BuildEmailLayout(
            "Season Registration Reminder",
            $@"<p style='{EmailTemplateHelper.Styles.Content}'>Hi {userName},</p>
            {(customMessage != null ? $"<div style='{EmailTemplateHelper.Styles.InfoBox}'><p style='{EmailTemplateHelper.Styles.InfoText}'>{customMessage}</p></div>" : "")}
            <div style='background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                <p style='{EmailTemplateHelper.Styles.Content}'><strong>Payment Instructions:</strong></p>
                {EmailTemplateHelper.BuildInfoBox(fields)}
            </div>"
        );

        await SendEmailAsync(userEmail, "Season Registration Reminder - Saint Henri Basketball", content);
    }

    public async Task SendSeasonStatusUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers)
    {
        var statusMessage = season.Status == SeasonStatus.Open ? "opened" : "closed";
        var fields = new Dictionary<string, string?>
        {
            { "Season Period", $"{season.StartDate:MMM dd, yyyy} - {season.EndDate:MMM dd, yyyy}" },
            { "Status", season.Status.ToString() },
            { "Price", $"${season.Price}" }
        };

        foreach (var user in registeredUsers)
        {
            var content = EmailTemplateHelper.BuildEmailLayout(
                "Season Status Update",
                $@"<p style='{EmailTemplateHelper.Styles.Content}'>The basketball season has been {statusMessage}:</p>
                {EmailTemplateHelper.BuildInfoBox(fields)}
                {(season.Status == SeasonStatus.Open
                    ? "<p style='color: #666; font-size: 14px;'>You can now register for sessions.</p>"
                    : "<p style='color: #666; font-size: 14px;'>Registration for new sessions is now closed.</p>")}"
            );

            await SendEmailAsync(user.Email, $"Season Status Update - Saint Henri Basketball", content);
        }
    }

    public async Task SendPaymentReminderEmailAsync(string? userEmail, string userName, PaymentPlan paymentPlan,
        string? customMessage = null)
    {
        var fields = new Dictionary<string, string?>
        {
            { "Payment Plan", paymentPlan.ToString() },
            { "Instructions", "Send payment via Interac e-Transfer to: pay@sainthenribasketball.com" }
        };

        var content = EmailTemplateHelper.BuildEmailLayout(
            "Payment Reminder",
            $@"<p style='{EmailTemplateHelper.Styles.Content}'>Hi {userName},</p>
           {(customMessage != null ? $"<div style='{EmailTemplateHelper.Styles.InfoBox}'><p style='{EmailTemplateHelper.Styles.InfoText}'>{customMessage}</p></div>" : "")}
           {EmailTemplateHelper.BuildInfoBox(fields)}"
        );

        await SendEmailAsync(userEmail, "Payment Reminder - Saint Henri Basketball", content);
    }

    public async Task SendSeasonUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers,
        string[] changedProperties)
    {
        var changes = string.Join(", ", changedProperties.Select(p => p.ToLower()));
        var fields = new Dictionary<string, string?>
        {
            { "Season Period", $"{season.StartDate:MMM dd, yyyy} - {season.EndDate:MMM dd, yyyy}" },
            { "Price", $"${season.Price}" }
        };

        if (!string.IsNullOrEmpty(season.Notes))
        {
            fields.Add("Notes", season.Notes);
        }

        foreach (var user in registeredUsers)
        {
            var content = EmailTemplateHelper.BuildEmailLayout(
                "Season Update",
                $@"<p style='{EmailTemplateHelper.Styles.Content}'>The following details have been updated: {changes}</p>
               {EmailTemplateHelper.BuildInfoBox(fields)}
               <p style='{EmailTemplateHelper.Styles.Content}'>If you have any questions about these changes, please contact us.</p>"
            );

            await SendEmailAsync(user.Email, "Season Update - Saint Henri Basketball", content);
        }
    }

    public async Task SendSeasonPaymentReminderEmailAsync(SeasonRegistration registration)
    {
        try
        {
            var userName = $"{registration.User.FirstName} {registration.User.LastName}";
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("America/Montreal"));

            var fields = new Dictionary<string, string?>
            {
                {
                    "Season Period",
                    $"{registration.Season.StartDate:MMM dd, yyyy} - {registration.Season.EndDate:MMM dd, yyyy}"
                },
                { "Amount Due", $"${registration.Season.Price}" },
                { "Registration Date", registration.RegisteredOn.ToString("MMM dd, yyyy") }
            };

            var paymentInstructions = new Dictionary<string, string?>
            {
                { "Send to", "pay@sainthenribasketball.com" },
                { "Amount", $"${registration.Season.Price}" },
                { "Message", $"Season Registration - {userName}" }
            };

            var content = EmailTemplateHelper.BuildEmailLayout(
                "Season Payment Reminder",
                $@"<p style='{EmailTemplateHelper.Styles.Content}'>Hi {userName},</p>
                <p style='{EmailTemplateHelper.Styles.Content}'>This is a friendly reminder about your season registration payment:</p>
                {EmailTemplateHelper.BuildInfoBox(fields)}
                <div style='background-color: #e8f5e9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    <h2 style='color: #2e7d32; margin-top: 0;'>Payment Instructions</h2>
                    {EmailTemplateHelper.BuildInfoBox(paymentInstructions)}
                    <p style='{EmailTemplateHelper.Styles.Content}'>Please complete your payment to secure your spot in the season.</p>
                </div>"
            );

            var msg = new SendGridMessage();
            msg.SetFrom(_fromAddress);
            msg.AddTo(new EmailAddress(registration.User.Email));
            msg.SetSubject("Season Payment Reminder - Saint Henri Basketball");
            msg.AddContent(MimeType.Html, content);

            try
            {
                var billGenerator = new BillPdfGenerator(_webHostEnvironment);
                var reference = $"SEASON-{DateTime.Now:yyMM}-{Random.Shared.Next(1000, 9999)}";

                var billDetails = new BillDetails
                {
                    Name = userName,
                    Email = registration.User.Email,
                    Description = "Season Registration Payment",
                    Amount = registration.Season.Price,
                    Reference = reference,
                    Date = localTime
                };

                var pdfContent = billGenerator.GenerateBill(billDetails, "en");

                msg.AddAttachment(
                    "season_registration_bill.pdf",
                    Convert.ToBase64String(pdfContent),
                    "application/pdf",
                    "attachment"
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate PDF bill. Sending email without attachment.");
            }

            await SendWithRetryAsync(msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send season payment reminder email for registration {RegistrationId}",
                registration.Id);
            throw;
        }
    }

    public async Task SendNewUserNotificationToAdminAsync(ApplicationUser newUser)
    {
        var fields = new Dictionary<string, string?>
        {
            { "Name", $"{newUser.FirstName} {newUser.LastName}" },
            { "Email", newUser.Email },
            { "Username", newUser.Username },
            { "Payment Plan", newUser.PaymentPlan.ToString() },
            { "Registration Date", DateTime.UtcNow.ToString("MMM dd, yyyy HH:mm") + " UTC" }
        };

        var content = EmailTemplateHelper.BuildEmailLayout(
            "New User Registration",
            $@"<p style='{EmailTemplateHelper.Styles.Content}'>A new user has registered for Saint Henri Basketball.</p>
           {EmailTemplateHelper.BuildInfoBox(fields)}
           <p style='{EmailTemplateHelper.Styles.Content}'>You can manage users from the admin dashboard.</p>"
        );

        try
        {
            await SendEmailAsync("admin@sainthenribasketball.com", "New User Registration - Saint Henri Basketball",
                content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin notification email for new user {Email}", newUser.Email);
        }
    }

    public async Task SendGeneralAnnouncementEmailAsync(string? userEmail, string userName, string message)
    {
        var content = EmailTemplateHelper.BuildEmailLayout(
            "Important Announcement",
            $@"<p style='{EmailTemplateHelper.Styles.Content}'>Hi {userName},</p>
           <div style='{EmailTemplateHelper.Styles.InfoBox}'>
               <p style='{EmailTemplateHelper.Styles.InfoText}'>{message}</p>
           </div>"
        );

        await SendEmailAsync(userEmail, "Announcement - Saint Henri Basketball", content);
    }

    #endregion

    #region Helper Methods

    private Task<string> BuildEmailContentAsync(EmailType emailType, ApplicationUser user, EmailLanguage language,
        string? customMessage, string? customMessageFr)
    {
        var userName = $"{user.FirstName} {user.LastName}";
        return Task.FromResult(language switch
        {
            EmailLanguage.English => BuildEmailContent(emailType, userName, user.PaymentPlan, customMessage),
            EmailLanguage.French => BuildEmailContent(emailType, userName, user.PaymentPlan,
                customMessageFr ?? customMessage, true),
            EmailLanguage.Bilingual => BuildBilingualContent(emailType, userName, user.PaymentPlan, customMessage,
                customMessageFr),
            _ => BuildEmailContent(emailType, userName, user.PaymentPlan, customMessage)
        });
    }

    private string BuildEmailContent(EmailType emailType, string userName, PaymentPlan paymentPlan,
        string? customMessage, bool isFrench = false)
    {
        var title = GetEmailTitle(emailType, isFrench);
        var greeting = isFrench ? $"Bonjour {userName}" : $"Hi {userName}";
        var content = customMessage ?? GetDefaultMessage(emailType, userName, paymentPlan, isFrench);

        return EmailTemplateHelper.BuildEmailLayout(
            title,
            $@"<p style='{EmailTemplateHelper.Styles.Content}'>{greeting},</p>
                <div style='{EmailTemplateHelper.Styles.InfoBox}'>
                    <p style='{EmailTemplateHelper.Styles.InfoText}'>{content}</p>
                </div>"
        );
    }

    private string BuildBilingualContent(EmailType emailType, string userName, PaymentPlan paymentPlan,
        string? customMessageEn, string? customMessageFr)
    {
        return EmailTemplateHelper.BuildBilingualContent(
            GetEmailTitle(emailType, false),
            GetEmailTitle(emailType, true),
            BuildEmailContent(emailType, userName, paymentPlan, customMessageEn),
            BuildEmailContent(emailType, userName, paymentPlan, customMessageFr, true)
        );
    }

    private string GetEmailTitle(EmailType emailType, bool isFrench) => (emailType, isFrench) switch
    {
        (EmailType.PaymentReminder, true) => "Rappel de paiement",
        (EmailType.PaymentReminder, false) => "Payment Reminder",
        (EmailType.PaymentConfirmation, true) => "Confirmation de paiement",
        (EmailType.PaymentConfirmation, false) => "Payment Confirmation",
        (EmailType.PaymentPlanUpdate, true) => "Mise à jour du plan de paiement",
        (EmailType.PaymentPlanUpdate, false) => "Payment Plan Update",
        (EmailType.AttendanceReminder, true) => "Rappel de présence",
        (EmailType.AttendanceReminder, false) => "Attendance Reminder",
        (EmailType.AttendanceConfirmation, true) => "Confirmation de présence",
        (EmailType.AttendanceConfirmation, false) => "Attendance Confirmation",
        (EmailType.AttendanceUpdate, true) => "Mise à jour de présence",
        (EmailType.AttendanceUpdate, false) => "Attendance Update",
        (EmailType.SeasonRegistrationReminder, true) => "Rappel d'inscription à la saison",
        (EmailType.SeasonRegistrationReminder, false) => "Season Registration Reminder",
        (EmailType.SeasonRegistrationConfirmation, true) => "Confirmation d'inscription à la saison",
        (EmailType.SeasonRegistrationConfirmation, false) => "Season Registration Confirmation",
        (EmailType.SeasonRegistrationCancelled, true) => "Annulation d'inscription à la saison",
        (EmailType.SeasonRegistrationCancelled, false) => "Season Registration Cancelled",
        (EmailType.SeasonStatusUpdate, true) => "Mise à jour du statut de la saison",
        (EmailType.SeasonStatusUpdate, false) => "Season Status Update",
        (EmailType.SeasonUpdate, true) => "Mise à jour de la saison",
        (EmailType.SeasonUpdate, false) => "Season Update",
        (EmailType.SeasonPaymentReminder, true) => "Rappel de paiement de la saison",
        (EmailType.SeasonPaymentReminder, false) => "Season Payment Reminder",
        (EmailType.GeneralAnnouncement, true) => "Annonce",
        (EmailType.GeneralAnnouncement, false) => "Announcement",
        (EmailType.FacilityUpdate, true) => "Mise à jour des installations",
        (EmailType.FacilityUpdate, false) => "Facility Update",
        (EmailType.ScheduleChange, true) => "Changement d'horaire",
        (EmailType.ScheduleChange, false) => "Schedule Change",
        (EmailType.LowAttendanceWarning, true) => "Avertissement de faible participation",
        (EmailType.LowAttendanceWarning, false) => "Low Attendance Warning",
        (EmailType.AdminNotification, true) => "Notification administrateur",
        (EmailType.AdminNotification, false) => "Admin Notification",
        _ => "Saint Henri Basketball"
    };

    private string GetEmailSubject(EmailType emailType, EmailLanguage language) => (emailType, language) switch
    {
        (EmailType.PaymentReminder, EmailLanguage.French) => "Rappel de paiement - Saint Henri Basketball",
        (EmailType.PaymentReminder, EmailLanguage.English) => "Payment Reminder - Saint Henri Basketball",
        (EmailType.PaymentReminder, EmailLanguage.Bilingual) =>
            "Payment Reminder / Rappel de paiement - Saint Henri Basketball",
        (EmailType.PaymentConfirmation, EmailLanguage.French) => "Confirmation de paiement - Saint Henri Basketball",
        (EmailType.PaymentConfirmation, EmailLanguage.English) => "Payment Confirmation - Saint Henri Basketball",
        (EmailType.PaymentConfirmation, EmailLanguage.Bilingual) =>
            "Payment Confirmation / Confirmation de paiement - Saint Henri Basketball",
        (EmailType.AttendanceReminder, EmailLanguage.French) => "Rappel de présence - Saint Henri Basketball",
        (EmailType.AttendanceReminder, EmailLanguage.English) => "Attendance Reminder - Saint Henri Basketball",
        (EmailType.AttendanceReminder, EmailLanguage.Bilingual) =>
            "Attendance Reminder / Rappel de présence - Saint Henri Basketball",
        (EmailType.SeasonRegistrationReminder, EmailLanguage.French) => "Rappel d'inscription - Saint Henri Basketball",
        (EmailType.SeasonRegistrationReminder, EmailLanguage.English) =>
            "Registration Reminder - Saint Henri Basketball",
        (EmailType.SeasonRegistrationReminder, EmailLanguage.Bilingual) =>
            "Registration Reminder / Rappel d'inscription - Saint Henri Basketball",
        (EmailType.GeneralAnnouncement, EmailLanguage.French) => "Annonce - Saint Henri Basketball",
        (EmailType.GeneralAnnouncement, EmailLanguage.English) => "Announcement - Saint Henri Basketball",
        (EmailType.GeneralAnnouncement, EmailLanguage.Bilingual) => "Announcement / Annonce - Saint Henri Basketball",
        _ => "Saint Henri Basketball"
    };

    private string GetDefaultMessage(EmailType emailType, string userName, PaymentPlan paymentPlan, bool isFrench) =>
        (emailType, isFrench) switch
        {
            (EmailType.PaymentReminder, true) => $"Votre paiement de ${GetPaymentAmount(paymentPlan)} est dû.",
            (EmailType.PaymentReminder, false) => $"Your payment of ${GetPaymentAmount(paymentPlan)} is due.",
            (EmailType.AttendanceReminder, true) => "Veuillez confirmer votre présence pour la prochaine séance.",
            (EmailType.AttendanceReminder, false) => "Please confirm your attendance for the upcoming session.",
            (EmailType.SeasonRegistrationReminder, true) => "N'oubliez pas de vous inscrire pour la prochaine saison.",
            (EmailType.SeasonRegistrationReminder, false) => "Don't forget to register for the upcoming season.",
            _ => string.Empty
        };

    private decimal GetPaymentAmount(PaymentPlan plan) => plan switch
    {
        PaymentPlan.Season => 95.00m,
        PaymentPlan.DropIn => 10.00m,
        _ => 0.00m
    };

    #endregion

    #region Bulk Email Methods

    public async Task<EmailSendResult> SendTargetedEmailsAsync(EmailType emailType, List<string> emails,
        EmailLanguage language, string? customMessage, string? customMessageFr)
    {
        var result = new EmailSendResult();

        foreach (var email in emails)
        {
            try
            {
                if (!new EmailAddressAttribute().IsValid(email))
                {
                    result.FailureCount++;
                    result.FailedEmails?.Add(email);
                    _logger.LogWarning("Invalid email format: {Email}", email);
                    continue;
                }

                var user = await _userRepository.GetByEmailAsync(email);

                var content = await BuildEmailContentAsync(emailType, user, language, customMessage, customMessageFr);
                var subject = GetEmailSubject(emailType, language);
                await SendEmailAsync(email, subject, content);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.FailedEmails?.Add(email);
                _logger.LogError(ex, "Failed to send email to {Email}", email);
            }
        }

        return result;
    }

    public async Task<EmailSendResult> SendPaymentRemindersAsync(List<string?> emails, EmailLanguage language,
        string? customMessage = null, string? customMessageFr = null)
    {
        if (!emails.Any())
            throw new ArgumentException("At least one email address is required", nameof(emails));

        return await SendTargetedEmailsAsync(EmailType.PaymentReminder, emails, language, customMessage,
            customMessageFr);
    }

    public async Task<EmailSendResult> SendAttendanceRemindersAsync(List<string?> emails, EmailLanguage language,
        string? customMessage = null, string? customMessageFr = null)
    {
        if (!emails.Any())
            throw new ArgumentException("At least one email address is required", nameof(emails));

        return await SendTargetedEmailsAsync(EmailType.AttendanceReminder, emails, language, customMessage,
            customMessageFr);
    }

    public async Task<EmailSendResult> SendSeasonRegistrationRemindersAsync(List<string?> emails,
        EmailLanguage language,
        string? customMessage = null, string? customMessageFr = null)
    {
        if (!emails.Any())
            throw new ArgumentException("At least one email address is required", nameof(emails));

        return await SendTargetedEmailsAsync(EmailType.SeasonRegistrationReminder, emails, language, customMessage,
            customMessageFr);
    }

    public async Task<EmailSendResult> SendGeneralAnnouncementsAsync(List<string?> emails, EmailLanguage language,
        string? customMessage = null, string? customMessageFr = null)
    {
        if (!emails.Any())
            throw new ArgumentException("At least one email address is required", nameof(emails));

        if (string.IsNullOrEmpty(customMessage))
            throw new ArgumentException("Message is required for general announcements", nameof(customMessage));

        if (language == EmailLanguage.French && string.IsNullOrEmpty(customMessageFr))
            throw new ArgumentException("French message is required when language is French", nameof(customMessageFr));

        if (language == EmailLanguage.Bilingual && string.IsNullOrEmpty(customMessageFr))
            throw new ArgumentException("French message is required when language is Bilingual",
                nameof(customMessageFr));

        return await SendTargetedEmailsAsync(EmailType.GeneralAnnouncement, emails, language, customMessage,
            customMessageFr);
    }

    #endregion
}