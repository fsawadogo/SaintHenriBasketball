using System.ComponentModel.DataAnnotations;
using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Hosting;
using Hangfire;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Templates;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly SendGridClient _client;
    private readonly EmailAddress _fromAddress;
    private readonly ILogger<EmailService> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private const int MaxRetries = 3;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger,
        IUserRepository userRepository,
        IPaymentRepository paymentRepository,
        IBackgroundJobClient backgroundJobs,
        IWebHostEnvironment webHostEnvironment, 
        ISessionRepository sessionRepository)
    {
        _logger = logger;
        _userRepository = userRepository;
        _backgroundJobs = backgroundJobs;
        _webHostEnvironment = webHostEnvironment;
        _sessionRepository = sessionRepository;
        _paymentRepository = paymentRepository;

        var apiKey = configuration["SendGrid:ApiKey"] 
                     ?? throw new ArgumentNullException(nameof(configuration), "SendGrid API key is not configured");
        var fromEmail = configuration["SendGrid:FromEmail"]
            ?? throw new ArgumentNullException(nameof(configuration), "SendGrid From Email is not configured");
        var fromName = configuration["SendGrid:FromName"]
            ?? throw new ArgumentNullException(nameof(configuration), "SendGrid From Name is not configured");

        _client = new SendGridClient(apiKey);
        _fromAddress = new EmailAddress(fromEmail, fromName);
    }

    #region Base Email Methods
    public async Task SendEmailAsync(string? to, string subject, string htmlContent)
    {
        if (string.IsNullOrEmpty(to))
            throw new ArgumentNullException(nameof(to), "Recipient email address cannot be null or empty");

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
        try
        {
            var user = await _userRepository.GetByEmailAsync(to)
                ?? throw new ArgumentException($"User not found for email: {to}");

            var content = EmailTemplates.Authentication.GetConfirmationEmail(
                $"{user.FirstName} {user.LastName}",
                confirmationLink
            );

            await SendEmailAsync(to, "Confirmez votre email - Saint Henri Basketball", content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send confirmation email to {Email}", to);
            throw;
        }
    }

    public async Task SendPasswordResetEmailAsync(string? to, string resetLink)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(to)
                ?? throw new ArgumentException($"User not found for email: {to}");

            var content = EmailTemplates.Authentication.GetPasswordResetEmail(
                $"{user.FirstName} {user.LastName}",
                resetLink
            );

            await SendEmailAsync(to, "Réinitialisation du mot de passe - Saint Henri Basketball", content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", to);
            throw;
        }
    }
    #endregion

    #region Payment Emails
    public async Task SendPaymentCreatedConfirmationAsync(Guid userId, decimal amount, string? reference, EmailLanguage emailLanguage)
    {
        var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new ArgumentException($"User not found for id: {userId}");

        try
        {
            var content = EmailTemplates.Payments.GetPaymentCreatedEmail(
                $"{user.FirstName}",
                amount,
                reference ?? GenerateReference("PAY")
            );

            await SendEmailAsync(
                user.Email,
                "Demande de paiement - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment created confirmation email to {Email}", user.Email);
            throw;
        }
    }

    public async Task SendPaymentConfirmationAsync(Guid userId, decimal amount, string? reference)
    {
        var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new ArgumentException($"User not found for id: {userId}");

        try
        {
            var userName = user.FirstName;
            var content = EmailTemplates.Payments.GetPaymentConfirmationEmail(
                userName,
                amount,
                reference ?? GenerateReference("PAY"),
                DateTime.UtcNow
            );

            var msg = new SendGridMessage();
            msg.SetFrom(_fromAddress);
            msg.AddTo(new EmailAddress(user.Email));
            msg.SetSubject("Confirmation de paiement - Saint Henri Basketball");
            msg.AddContent(MimeType.Html, content);

            try
            {
                var billGenerator = new BillPdfGenerator(_webHostEnvironment);
                var description = user.PaymentPlan == PaymentPlan.Season
                    ? "Forfait de saison"
                    : "Forfait à la séance";

                var billDetails = new BillDetails
                {
                    Name = userName,
                    Email = user.Email,
                    Description = description,
                    Amount = amount,
                    Reference = reference,
                    Date = DateTime.UtcNow
                };

                var pdfContent = billGenerator.GenerateBill(billDetails);
                msg.AddAttachment($"facture_{reference}.pdf", Convert.ToBase64String(pdfContent));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate PDF bill. Sending email without attachment.");
            }

            await SendWithRetryAsync(msg);
            await NotifyAdminOfPayment(user.Email, userName, amount, reference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment confirmation email to {Email}", user.Email);
            throw;
        }
    }

    private async Task NotifyAdminOfPayment(string? userEmail, string userName, decimal amount, string? reference)
    {
        try
        {
            var content = EmailTemplates.Payments.GetPaymentConfirmationEmail(
                userName,
                amount,
                reference ?? GenerateReference("PAY"),
                DateTime.UtcNow
            );

            await SendEmailAsync(
                "admin@sainthenribasketball.com",
                "Nouveau paiement reçu - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment notification to admin for user {Email}", userEmail);
        }
    }

    public async Task SendPaymentReminderEmailAsync(Guid userId, PaymentPlan paymentPlan, string? customMessage = null)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new ArgumentException($"User not found for ID: {userId}");
        try
        {
            var payment = await _paymentRepository.GetPaymentsByUserAsync(userId)
                ?? throw new ArgumentException($"Payment not found for email: {user.Email}");
            var amount = GetPaymentAmount(paymentPlan);
            var content = EmailTemplates.Payments.GetPaymentReminderEmail(user.FirstName, amount, user.PaymentPlan);

            await SendEmailAsync(
                user.Email,
                "Rappel de paiement - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment reminder email to {Email}", user.Email);
            throw;
        }
    }
    
       public async Task SendPaymentCreatedConfirmationAsync(string? email, decimal amount, string? reference, EmailLanguage language)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(email)
                ?? throw new ArgumentException($"User not found for email: {email}");

            var content = EmailTemplates.Payments.GetPaymentCreatedEmail(
                $"{user.FirstName} {user.LastName}",
                amount,
                reference ?? GenerateReference("PAY")
            );

            await SendEmailAsync(
                email,
                "Demande de paiement - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment created confirmation email to {Email}", email);
            throw;
        }
    }

    public async Task SendPaymentConfirmationAsync(Guid userId, decimal amount, string? reference, EmailLanguage language)
    {
        var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new ArgumentException($"User not found for id: {userId}");
        try
        {
            var userName = user.FirstName;
            var content = EmailTemplates.Payments.GetPaymentConfirmationEmail(
                userName,
                amount,
                reference ?? GenerateReference("PAY"),
                DateTime.UtcNow
            );

            var msg = new SendGridMessage();
            msg.SetFrom(_fromAddress);
            msg.AddTo(new EmailAddress(user.Email));
            msg.SetSubject("Confirmation de paiement - Saint Henri Basketball");
            msg.AddContent(MimeType.Html, content);

            try
            {
                var billGenerator = new BillPdfGenerator(_webHostEnvironment);
                var description = user.PaymentPlan == PaymentPlan.Season
                    ? "Forfait de saison"
                    : "Forfait à la séance";

                var billDetails = new BillDetails
                {
                    Name = userName,
                    Email = user.Email,
                    Description = description,
                    Amount = amount,
                    Reference = reference,
                    Date = DateTime.UtcNow
                };

                var pdfContent = billGenerator.GenerateBill(billDetails);
                msg.AddAttachment($"facture_{reference}.pdf", Convert.ToBase64String(pdfContent));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate PDF bill. Sending email without attachment.");
            }

            await SendWithRetryAsync(msg);
            await NotifyAdminOfPayment(user.Email, userName, amount, reference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment confirmation email to {Email}", user);
            throw;
        }
    }

    public async Task SendPaymentPlanUpdateEmailAsync(Guid userId, PaymentPlan paymentPlan)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new ArgumentException($"User not found for ID: {userId}");

        try
        {
            var content = EmailTemplates.Payments.GetPaymentReminderEmail(
                user.FirstName,
                GetPaymentAmount(paymentPlan),
                user.PaymentPlan,
                $"Votre plan de paiement a été mis à jour vers: {GetPaymentPlanName(paymentPlan)}"
            );

            await SendEmailAsync(
                user.Email,
                "Mise à jour du plan de paiement - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment plan update email to {Email}", user.Email);
            throw;
        }
    }
    
    public async Task<EmailSendResult> SendPaymentRemindersAsync(
        List<string?> emails,
        EmailLanguage language,
        string? customMessage = null,
        string? customMessageFr = null)
    {
        var validEmails = emails.Where(e => !string.IsNullOrEmpty(e)).Select(e => e!).ToList();
        
        if (!validEmails.Any())
            throw new ArgumentException("At least one valid email address is required", nameof(emails));

        return await SendTargetedEmailsAsync(
            EmailType.PaymentReminder,
            validEmails,
            customMessageFr ?? customMessage
        );
    }
    #endregion

    #region Attendance Emails
    public async Task SendAttendanceConfirmationEmailAsync(SessionAttendance attendance)
    {
        try
        {
            var content = EmailTemplates.Attendance.GetAttendanceConfirmationEmail(
                $"{attendance.User.FirstName} {attendance.User.LastName}",
                attendance.Session.SessionDate,
                attendance.Session.StartTime,
                attendance.Session.EndTime,
                attendance.Session.Location,
                attendance.IsAttending,
                attendance.Notes
            );

            await SendEmailAsync(
                attendance.User.Email,
                "Confirmation de présence - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send attendance confirmation email for session {SessionId}", attendance.SessionId);
            throw;
        }
    }

    public async Task<EmailSendResult> SendAttendanceRemindersAsync(
        List<string?> emails,
        EmailLanguage language,
        string? customMessage = null,
        string? customMessageFr = null)
    {
        var validEmails = emails.Where(e => !string.IsNullOrEmpty(e)).Select(e => e!).ToList();
        
        if (!validEmails.Any())
            throw new ArgumentException("At least one valid email address is required", nameof(emails));

        return await SendTargetedEmailsAsync(
            EmailType.AttendanceReminder,
            validEmails,
            customMessageFr ?? customMessage
        );
    }

    public async Task SendAttendanceUpdateEmailAsync(SessionAttendance attendance)
    {
        try
        {
            var content = EmailTemplates.Attendance.GetAttendanceConfirmationEmail(
                $"{attendance.User.FirstName} {attendance.User.LastName}",
                attendance.Session.SessionDate,
                attendance.Session.StartTime.ToString(),
                attendance.Session.EndTime,
                attendance.Session.Location,
                attendance.IsAttending,
                attendance.Notes
            );

            await SendEmailAsync(
                attendance.User.Email,
                "Mise à jour de présence - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send attendance update email for session {SessionId}", attendance.SessionId);
            throw;
        }
    }

    public async Task SendAttendanceReminderEmailAsync(Guid userId, string? customMessage = null)
    {
        var nextSession = await _sessionRepository.GetNextSessionAsync()
                              ?? throw new InvalidOperationException("Aucune session à venir n'a été trouvée");
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new ArgumentException($"User not found for ID: {userId}");

        try
        {
            var content = EmailTemplates.Attendance.GetAttendanceReminderEmail(
                user.Id,
                nextSession.Id,
                nextSession.SessionDate,
                $"{user.FirstName} {user.LastName}",
                nextSession.StartTime,
                nextSession.EndTime,
                nextSession.Location,
                customMessage
            );

            await SendEmailAsync(
                user.Email,
                "Rappel de présence - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send attendance reminder email to {Email}", user.Email);
            throw;
        }
    }
    #endregion

    #region Season Emails
    public async Task SendSeasonRegistrationConfirmationEmailAsync(SeasonRegistration registration)
    {
        try
        {
            var content = EmailTemplates.Season.GetSeasonRegistrationConfirmationEmail(
                $"{registration.User.FirstName} {registration.User.LastName}",
                registration.Season.StartDate,
                registration.Season.EndDate,
                registration.Season.Price
            );

            await SendEmailAsync(
                registration.User.Email,
                "Confirmation d'inscription à la saison - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send season registration confirmation email to {Email}", registration.User.Email);
            throw;
        }
    }

    public async Task SendSeasonRegistrationCancelledEmailAsync(string? userEmail, Season season)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(userEmail)
                ?? throw new ArgumentException($"User not found for email: {userEmail}");

            var content = EmailTemplates.Season.GetSeasonCancellationEmail(
                $"{user.FirstName} {user.LastName}",
                season.StartDate,
                season.EndDate
            );

            await SendEmailAsync(
                userEmail,
                "Annulation d'inscription à la saison - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send season registration cancellation email to {Email}", userEmail);
            throw;
        }
    }

    public async Task SendSeasonRegistrationReminderEmailAsync(string? userEmail, string userName, string? customMessage = null)
    {
        try
        {
            var content = EmailTemplates.General.GetAnnouncementEmail(
                userName,
                "N'oubliez pas de vous inscrire pour la prochaine saison de basketball!",
                customMessage
            );

            await SendEmailAsync(
                userEmail,
                "Rappel d'inscription à la saison - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send season registration reminder email to {Email}", userEmail);
            throw;
        }
    }
    #endregion

    #region Admin Notifications
    public async Task SendNewUserNotificationToAdminAsync(ApplicationUser newUser)
    {
        try
        {
            var content = EmailTemplates.General.GetAnnouncementEmail(
                "Admin",
                $"Un nouvel utilisateur s'est inscrit:\n\nNom: {newUser.FirstName} {newUser.LastName}\nEmail: {newUser.Email}\nPlan: {newUser.PaymentPlan}"
            );

            await SendEmailAsync(
                "admin@sainthenribasketball.com",
                "Nouvel utilisateur inscrit - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send new user notification email for {Email}", newUser.Email);
        }
    }
    #endregion

    #region General Methods
    public async Task SendGeneralAnnouncementEmailAsync(string? userEmail, string userName, string message)
    {
        try
        {
            var content = EmailTemplates.General.GetAnnouncementEmail(userName, message);

            await SendEmailAsync(
                userEmail,
                "Annonce importante - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send announcement email to {Email}", userEmail);
            throw;
        }
    }
    #endregion

    #region Helper Methods
    private static decimal GetPaymentAmount(PaymentPlan plan) => plan switch
    {
        PaymentPlan.Season => 95.00m,
        PaymentPlan.DropIn => 10.00m,
        _ => throw new ArgumentException($"Invalid payment plan: {plan}")
    };
    
    private static string GetPaymentPlanName(PaymentPlan plan) => plan switch
    {
        PaymentPlan.Season => "Forfait de saison",
        PaymentPlan.DropIn => "Forfait à la séance",
        _ => plan.ToString()
    };
    private static string GenerateReference(string prefix)
    {
        return $"{prefix}-{DateTime.Now:yyMM}-{Random.Shared.Next(1000, 9999)}";
    }
    #endregion

    #region Bulk Email Methods
    public async Task<EmailSendResult> SendTargetedEmailsAsync(
        EmailType emailType,
        List<string> emails,
        string? customMessage = null)
    {
        var result = new EmailSendResult();
        var nextSession = await _sessionRepository.GetNextSessionAsync()
                          ?? throw new InvalidOperationException("Aucune session à venir n'a été trouvée");
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

                string content = emailType switch
                {
                    EmailType.PaymentReminder => EmailTemplates.Payments.GetPaymentReminderEmail(
                        $"{user.FirstName}",
                        GetPaymentAmount(user.PaymentPlan),
                        user.PaymentPlan,
                        customMessage),

                
                    EmailType.AttendanceReminder => EmailTemplates.Attendance.GetAttendanceReminderEmail(
                        user.Id,
                        nextSession.Id,
                        nextSession.SessionDate,
                        user.Username,
                        nextSession.StartTime,
                        nextSession.EndTime,
                        nextSession.Location,
                        customMessage
                    ),
                        
                    EmailType.SeasonRegistrationReminder => EmailTemplates.General.GetAnnouncementEmail(
                        $"{user.FirstName} {user.LastName}",
                        "N'oubliez pas de vous inscrire pour la prochaine saison de basketball!",
                        customMessage),
                        
                    EmailType.GeneralAnnouncement => EmailTemplates.General.GetAnnouncementEmail(
                        $"{user.FirstName} {user.LastName}",
                        customMessage ?? "Annonce importante de Saint Henri Basketball"),
                        
                    _ => throw new ArgumentException($"Unsupported email type: {emailType}")
                };

                string subject = emailType switch
                {
                    EmailType.PaymentReminder => "Rappel de paiement - Saint Henri Basketball",
                    EmailType.AttendanceReminder => "Rappel de présence - Saint Henri Basketball",
                    EmailType.SeasonRegistrationReminder => "Rappel d'inscription - Saint Henri Basketball",
                    EmailType.GeneralAnnouncement => "Annonce importante - Saint Henri Basketball",
                    _ => "Saint Henri Basketball"
                };

                await SendEmailAsync(email, subject, content);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.FailedEmails.Add(email);
                _logger.LogError(ex, "Failed to send {EmailType} email to {Email}", emailType, email);
            }
        }

        return result;
    }

    public async Task<EmailSendResult> SendPaymentRemindersAsync(List<string> emails, string? customMessage = null)
    {
        if (!emails.Any())
            throw new ArgumentException("At least one email address is required", nameof(emails));

        return await SendTargetedEmailsAsync(EmailType.PaymentReminder, emails, customMessage);
    }

    public async Task<EmailSendResult> SendAttendanceRemindersAsync(List<string> emails, string? customMessage = null)
    {
        if (!emails.Any())
            throw new ArgumentException("At least one email address is required", nameof(emails));

        return await SendTargetedEmailsAsync(EmailType.AttendanceReminder, emails, customMessage);
    }

    public async Task<EmailSendResult> SendSeasonRegistrationRemindersAsync(List<string> emails, string? customMessage = null)
    {
        if (!emails.Any())
            throw new ArgumentException("At least one email address is required", nameof(emails));

        return await SendTargetedEmailsAsync(EmailType.SeasonRegistrationReminder, emails, customMessage);
    }

    public async Task<EmailSendResult> SendGeneralAnnouncementsAsync(List<string> emails, string message)
    {
        if (!emails.Any())
            throw new ArgumentException("At least one email address is required", nameof(emails));

        if (string.IsNullOrEmpty(message))
            throw new ArgumentException("Message is required for general announcements", nameof(message));

        return await SendTargetedEmailsAsync(EmailType.GeneralAnnouncement, emails, message);
    }
    #endregion

    #region Season Updates
    public async Task SendSeasonStatusUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers)
    {
        foreach (var user in registeredUsers)
        {
            try
            {
                var content = EmailTemplates.General.GetAnnouncementEmail(
                    user.FirstName,
                    $"Le statut de la saison a été mis à jour à: {season.Status}",
                    $"Période: {season.StartDate:dd MMMM yyyy} - {season.EndDate:dd MMMM yyyy}\nPrix: {season.Price:C}"
                );

                await SendEmailAsync(
                    user.Email,
                    "Mise à jour du statut de la saison - Saint Henri Basketball",
                    content
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send season status update email to {Email}", user.Email);
            }
        }
    }

    public async Task SendSeasonUpdateEmailAsync(Season season, List<SeasonUserDto> registeredUsers, string[] changedProperties)
    {
        foreach (var user in registeredUsers)
        {
            try
            {
                var changes = string.Join(", ", changedProperties.Select(p => p.ToLower()));
                var content = EmailTemplates.General.GetAnnouncementEmail(
                    user.FirstName,
                    $"Les détails suivants ont été mis à jour: {changes}",
                    $"Période: {season.StartDate:dd MMMM yyyy} - {season.EndDate:dd MMMM yyyy}\nPrix: {season.Price:C}\n\n{season.Notes}"
                );

                await SendEmailAsync(
                    user.Email,
                    "Mise à jour de la saison - Saint Henri Basketball",
                    content
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send season update email to {Email}", user.Email);
            }
        }
    }

    public async Task SendSeasonPaymentReminderEmailAsync(SeasonRegistration registration)
    {
        try
        {
            var userName = $"{registration.User.FirstName} {registration.User.LastName}";
            var content = EmailTemplates.Payments.GetPaymentReminderEmail(
                userName,
                registration.Season.Price,
                PaymentPlan.Season,
                "Veuillez effectuer le paiement pour confirmer votre inscription à la saison."
            );

            await SendEmailAsync(
                registration.User.Email,
                "Rappel de paiement pour la saison - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send season payment reminder email for registration {RegistrationId}", registration.Id);
            throw;
        }
    }
    
    public async Task<EmailSendResult> SendSeasonRegistrationRemindersAsync(
        List<string?> emails,
        EmailLanguage language,
        string? customMessage = null,
        string? customMessageFr = null)
    {
        var validEmails = emails.Where(e => !string.IsNullOrEmpty(e)).Select(e => e!).ToList();
        
        if (!validEmails.Any())
            throw new ArgumentException("At least one valid email address is required", nameof(emails));

        return await SendTargetedEmailsAsync(
            EmailType.SeasonRegistrationReminder,
            validEmails,
            customMessageFr ?? customMessage
        );
    }
    #endregion

    public async Task<EmailSendResult> SendTargetedEmailsAsync(
        EmailType emailType,
        List<string> emails,
        EmailLanguage language,
        string? customMessage,
        string? customMessageFr)
    {
        // Ignore language parameter and use customMessage
        // If customMessageFr is provided, use it instead of customMessage
        return await SendTargetedEmailsAsync(
            emailType,
            emails,
            customMessageFr ?? customMessage
        );
    }


    #region General
    public async Task<EmailSendResult> SendGeneralAnnouncementsAsync(
        List<string?> emails,
        EmailLanguage language,
        string? customMessage = null,
        string? customMessageFr = null)
    {
        var validEmails = emails.Where(e => !string.IsNullOrEmpty(e)).Select(e => e!).ToList();
        
        if (!validEmails.Any())
            throw new ArgumentException("At least one valid email address is required", nameof(emails));

        if (string.IsNullOrEmpty(customMessage) && string.IsNullOrEmpty(customMessageFr))
            throw new ArgumentException("Either customMessage or customMessageFr is required for general announcements");

        return await SendTargetedEmailsAsync(
            EmailType.GeneralAnnouncement,
            validEmails,
            customMessageFr ?? customMessage
        );
    }
    #endregion
    
}