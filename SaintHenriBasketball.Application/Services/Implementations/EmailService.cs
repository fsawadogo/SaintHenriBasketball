using System.ComponentModel.DataAnnotations;
using Resend;
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
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Templates;
using SaintHenriBasketball.Application.DTOs.Session;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly string _fromAddress;
    private readonly ILogger<EmailService> _logger;
    private readonly IUserRepository _userRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IGenericRepository<EmailLog> _emailLogRepository;
    private const int MaxRetries = 3;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger,
        IUserRepository userRepository,
        IPaymentRepository paymentRepository,
        IWebHostEnvironment webHostEnvironment,
        ISessionRepository sessionRepository,
        IResend resend,
        IGenericRepository<EmailLog> emailLogRepository)
    {
        _logger = logger;
        _userRepository = userRepository;
        _webHostEnvironment = webHostEnvironment;
        _sessionRepository = sessionRepository;
        _paymentRepository = paymentRepository;
        _resend = resend;
        _emailLogRepository = emailLogRepository;

        var fromEmail = configuration["Resend:FromEmail"]
            ?? throw new ArgumentNullException(nameof(configuration), "Resend From Email is not configured");
        var fromName = configuration["Resend:FromName"]
            ?? throw new ArgumentNullException(nameof(configuration), "Resend From Name is not configured");

        _fromAddress = $"{fromName} <{fromEmail}>";
    }

    #region Base Email Methods
    public async Task SendEmailAsync(string? to, string subject, string htmlContent)
    {
        if (string.IsNullOrEmpty(to))
            throw new ArgumentNullException(nameof(to), "Recipient email address cannot be null or empty");

        var message = new EmailMessage { From = _fromAddress, Subject = subject, HtmlBody = htmlContent };
        message.To.Add(to);

        await SendWithRetryAsync(message, to, subject);
    }

    public async Task SendEmailWithAttachmentAsync(string? to, string subject, string htmlContent, string attachmentFilename, byte[] attachmentContent)
    {
        if (string.IsNullOrEmpty(to))
            throw new ArgumentNullException(nameof(to), "Recipient email address cannot be null or empty");

        var message = new EmailMessage
        {
            From = _fromAddress,
            Subject = subject,
            HtmlBody = htmlContent,
            Attachments = new List<EmailAttachment>
            {
                new EmailAttachment { Filename = attachmentFilename, Content = attachmentContent }
            }
        };
        message.To.Add(to);

        await SendWithRetryAsync(message, to, subject);
    }

    /// <summary>Unified retry loop for all email sends. Logs success/failure to EmailLog.</summary>
    private async Task SendWithRetryAsync(EmailMessage message, string to, string subject, EmailType emailType = EmailType.GeneralAnnouncement)
    {
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await _resend.EmailSendAsync(message);

                // Log successful send
                try { await _emailLogRepository.AddAsync(new EmailLog(to, subject, emailType)); }
                catch (Exception logEx) { _logger.LogWarning(logEx, "Failed to log email send to {Email}", to); }

                return; // Success — exit
            }
            catch (Exception ex)
            {
                if (attempt < MaxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    continue;
                }

                // Final failure — log and throw
                try
                {
                    var log = new EmailLog(to, subject, emailType);
                    log.MarkFailed(ex.Message);
                    await _emailLogRepository.AddAsync(log);
                }
                catch (Exception logEx) { _logger.LogWarning(logEx, "Failed to log email failure for {Email}", to); }

                _logger.LogError(ex, "Error sending email to {Email} after {MaxRetries} retries", to, MaxRetries);
                throw;
            }
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
                $"{user.FirstName}",
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
                $"{user.FirstName}",
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

    public async Task SendAccountCreatedEmailAsync(string? to, string password, string loginLink)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(to)
                ?? throw new ArgumentException($"User not found for email: {to}");

            var content = EmailTemplates.Authentication.GetAccountCreatedEmail(
                $"{user.FirstName}",
                password,
                loginLink
            );

            await SendEmailAsync(to, "Votre compte a été créé - Saint Henri Basketball", content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send account created email to {Email}", to);
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
            var actualReference = reference ?? GenerateReference("PAY");
            var content = EmailTemplates.Payments.GetPaymentConfirmationEmail(
                userName,
                amount,
                actualReference,
                DateTime.UtcNow
            );

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
                await SendEmailWithAttachmentAsync(
                    user.Email,
                    "Confirmation de paiement - Saint Henri Basketball",
                    content,
                    $"facture_{actualReference}.pdf",
                    pdfContent
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate PDF bill. Sending email without attachment.");
                await SendEmailAsync(user.Email, "Confirmation de paiement - Saint Henri Basketball", content);
            }

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
            var content = EmailTemplates.Admin.GetAdminNotificationEmail(
                "Admin",
                "Nouveau paiement reçu",
                $"Un paiement de {amount:C} a été reçu de {userName} (référence: {reference ?? "N/A"}).",
                "https://sainthenribasketball.com/admin/payments",
                "Voir les paiements"
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
            var content = EmailTemplates.Payments.GetPaymentReminderEmail(user.FirstName, amount, user.PaymentPlan, customMessage);

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
            var actualReference = reference ?? GenerateReference("PAY");
            var content = EmailTemplates.Payments.GetPaymentConfirmationEmail(
                userName,
                amount,
                actualReference,
                DateTime.UtcNow
            );

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
                await SendEmailWithAttachmentAsync(
                    user.Email,
                    "Confirmation de paiement - Saint Henri Basketball",
                    content,
                    $"facture_{actualReference}.pdf",
                    pdfContent
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate PDF bill. Sending email without attachment.");
                await SendEmailAsync(user.Email, "Confirmation de paiement - Saint Henri Basketball", content);
            }

            await NotifyAdminOfPayment(user.Email, userName, amount, reference);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment confirmation email to {Email}", user.Email);
            throw;
        }
    }

    public async Task SendPaymentPlanUpdateEmailAsync(Guid userId, PaymentPlan newPaymentPlan)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new ArgumentException($"User not found for ID: {userId}");

        try
        {
            var content = EmailTemplates.Payments.GetPaymentPlanUpdateEmail(
                user.FirstName,
                newPaymentPlan,
                GetPaymentAmount(newPaymentPlan),
                DateTime.UtcNow,
                "Votre forfait a été mis à jour. Si vous avez des questions, n'hésitez pas à nous contacter."
            );

            await SendEmailAsync(
                user.Email,
                "Mise à jour du forfait - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send payment plan update email to {Email}", user.Email);
            throw;
        }
    }

    public async Task SendDropInPaymentLinkEmailAsync(string? userEmail, string userName,
        Guid sessionId, decimal amount, DateTime sessionDate, string startTime, string endTime,
        EmailLanguage language = EmailLanguage.Bilingual)
    {
        if (string.IsNullOrEmpty(userEmail)) return;

        try
        {
            var paymentUrl = $"https://sainthenribasketball.com/drop-in-payment?sessionId={sessionId}";
            var interacEmail = "pay@sainthenribasketball.com";
            var dateStr = sessionDate.ToString("MMMM d, yyyy");

            var htmlContent = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #f97316;'>Drop-In Payment / Paiement à la carte</h2>

                    <p>Hello {userName},</p>
                    <p>Thank you for confirming your attendance for the session on <strong>{dateStr}</strong> from <strong>{startTime}</strong> to <strong>{endTime}</strong>.</p>
                    <p>Please complete your payment of <strong>${amount:F2}</strong>.</p>

                    <h3 style='color: #f97316;'>Interac e-Transfer (Recommended)</h3>
                    <ol>
                        <li>Open your banking app</li>
                        <li>Send <strong>${amount:F2}</strong> to <strong>{interacEmail}</strong></li>
                        <li>In the message, include: <strong>{userName} - Drop-in {dateStr}</strong></li>
                    </ol>

                    <p>Or pay by card: <a href='{paymentUrl}' style='color: #f97316;'>{paymentUrl}</a></p>

                    <hr style='border-color: #333; margin: 20px 0;'/>

                    <p>Bonjour {userName},</p>
                    <p>Merci d'avoir confirmé votre présence pour la session du <strong>{dateStr}</strong> de <strong>{startTime}</strong> à <strong>{endTime}</strong>.</p>
                    <p>Veuillez compléter votre paiement de <strong>{amount:F2}$</strong>.</p>

                    <h3 style='color: #f97316;'>Virement Interac (Recommandé)</h3>
                    <ol>
                        <li>Ouvrez votre application bancaire</li>
                        <li>Envoyez <strong>{amount:F2}$</strong> à <strong>{interacEmail}</strong></li>
                        <li>Dans le message, inscrivez : <strong>{userName} - Drop-in {dateStr}</strong></li>
                    </ol>

                    <p>Ou payez par carte : <a href='{paymentUrl}' style='color: #f97316;'>{paymentUrl}</a></p>
                </div>";

            await SendEmailAsync(
                userEmail,
                "Drop-In Payment / Paiement à la carte - Saint Henri Basketball",
                htmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send drop-in payment link email to {Email}", userEmail);
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
                $"{attendance.User.FirstName}",
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

    public async Task SendAttendanceUpdateEmailAsync(SessionAttendance attendance, bool previousStatus, string? reason = null)
    {
        try
        {
            var content = EmailTemplates.Attendance.GetAttendanceUpdateEmail(
                $"{attendance.User.FirstName}",
                attendance.Session.SessionDate,
                attendance.Session.StartTime,
                attendance.Session.EndTime,
                attendance.Session.Location,
                previousStatus,
                attendance.IsAttending,
                reason ?? attendance.Notes
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
                $"{user.FirstName}",
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

    public async Task SendLowAttendanceWarningEmailAsync(SessionDto session, List<UserDto> registeredUsers)
    {
        foreach (var user in registeredUsers)
        {
            try
            {
                var content = EmailTemplates.General.GetLowAttendanceWarningEmail(
                    $"{user.FirstName} {user.LastName}",
                    session.SessionDate,
                    session.StartTime,
                    session.Location ?? string.Empty);

                await SendEmailAsync(
                    user.Email,
                    "Alerte: Faible participation à la session - Saint Henri Basketball",
                    content
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send low attendance warning email to {Email}", user.Email);
            }
        }
    }
    #endregion

    #region Session Emails
    public async Task SendSessionCancellationEmailAsync(Session session, List<ApplicationUser> registeredUsers, string? cancellationReason = null, Session? alternativeSession = null)
    {
        foreach (var user in registeredUsers)
        {
            try
            {
                var content = EmailTemplates.Sessions.GetSessionCancellationEmail(
                    $"{user.FirstName}",
                    session.SessionDate,
                    session.StartTime,
                    session.Location,
                    cancellationReason,
                    alternativeSession?.Id
                );

                await SendEmailAsync(
                    user.Email,
                    "Session annulée - Saint Henri Basketball",
                    content
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send session cancellation email to {Email}", user.Email);
            }
        }
    }

    private SessionDto MapToSessionDto(Session session)
    {
        return new SessionDto
        {
            Id = session.Id,
            SessionDate = session.SessionDate,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Location = session.Location ?? string.Empty,
        };
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

    public async Task SendSeasonRegistrationReminderEmailAsync(string? userEmail, Season season, string? customMessage = null)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(userEmail)
                ?? throw new ArgumentException($"User not found for email: {userEmail}");

            var content = EmailTemplates.Season.GetSeasonRegistrationReminderEmail(
                $"{user.FirstName} {user.LastName}",
                season.Name,
                season.StartDate,
                season.EndDate,
                season.Price,
                $"https://sainthenribasketball.com/season/{season.Id}/register",
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
            var content = EmailTemplates.Admin.GetNewUserNotificationEmail(
                "Admin",
                $"{newUser.FirstName}",
                newUser.Email,
                DateTime.UtcNow,
                GetPaymentPlanName(newUser.PaymentPlan)
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

    public async Task SendAdminNotificationAsync(string subject, string message, string? actionLink = null, string? actionText = null)
    {
        try
        {
            var content = EmailTemplates.Admin.GetAdminNotificationEmail(
                "Admin",
                subject,
                message,
                actionLink,
                actionText
            );

            await SendEmailAsync(
                "admin@sainthenribasketball.com",
                $"Admin: {subject} - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin notification email: {Subject}", subject);
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

    public async Task SendScheduleChangeEmailAsync(Session session, List<ApplicationUser> affectedUsers, string details, DateTime? newDate = null, TimeSpan? newTime = null)
    {
        foreach (var user in affectedUsers)
        {
            try
            {
                var content = EmailTemplates.General.GetScheduleChangeEmail(
                    $"{user.FirstName}",
                    details,
                    newDate,
                    newTime
                );

                await SendEmailAsync(
                    user.Email,
                    "Changement d'horaire - Saint Henri Basketball",
                    content
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send schedule change email to {Email}", user.Email);
            }
        }
    }

    public async Task SendFacilityUpdateEmailAsync(string? userEmail, string facilityName, string updateDetails, DateTime effectiveDate, string? alternativeFacility = null)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(userEmail)
                ?? throw new ArgumentException($"User not found for email: {userEmail}");

            var content = EmailTemplates.General.GetFacilityUpdateEmail(
                $"{user.FirstName}",
                facilityName,
                updateDetails,
                effectiveDate,
                alternativeFacility
            );

            await SendEmailAsync(
                userEmail,
                "Mise à jour des installations - Saint Henri Basketball",
                content
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send facility update email to {Email}", userEmail);
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
        return $"{prefix}-{DateTime.UtcNow:yyMM}-{Random.Shared.Next(1000, 9999)}";
    }
    #endregion

    #region Bulk Email Methods
    public async Task<EmailSendResult> SendTargetedEmailsAsync(
        EmailType emailType,
        List<string> emails,
        string? customMessage = null)
    {
        var result = new EmailSendResult();
        Session? nextSession = null;
        if (emailType == EmailType.AttendanceReminder)
        {
            nextSession = await _sessionRepository.GetNextSessionAsync()
                          ?? throw new InvalidOperationException("Aucune session à venir n'a été trouvée");
        }

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
                        user.FirstName,
                        nextSession.StartTime,
                        nextSession.EndTime,
                        nextSession.Location,
                        customMessage
                    ),

                    EmailType.SeasonRegistrationReminder => EmailTemplates.General.GetAnnouncementEmail(
                        $"{user.FirstName}",
                        "N'oubliez pas de vous inscrire pour la prochaine saison de basketball!",
                        customMessage),

                    EmailType.GeneralAnnouncement => EmailTemplates.General.GetAnnouncementEmail(
                        $"{user.FirstName}",
                        customMessage ?? "Annonce importante de Saint Henri Basketball"),

                    EmailType.ScheduleChange => EmailTemplates.General.GetScheduleChangeEmail(
                        $"{user.FirstName}",
                        customMessage ?? "Un changement d'horaire a été effectué."),

                    EmailType.FacilityUpdate => EmailTemplates.General.GetFacilityUpdateEmail(
                        $"{user.FirstName}",
                        "Saint Henri Basketball",
                        customMessage ?? "Une mise à jour des installations a été effectuée.",
                        DateTime.UtcNow),

                    _ => throw new ArgumentException($"Unsupported email type: {emailType}")
                };

                string subject = emailType switch
                {
                    EmailType.PaymentReminder => "Rappel de paiement - Saint Henri Basketball",
                    EmailType.AttendanceReminder => "Rappel de présence - Saint Henri Basketball",
                    EmailType.SeasonRegistrationReminder => "Rappel d'inscription - Saint Henri Basketball",
                    EmailType.GeneralAnnouncement => "Annonce importante - Saint Henri Basketball",
                    EmailType.ScheduleChange => "Changement d'horaire - Saint Henri Basketball",
                    EmailType.FacilityUpdate => "Mise à jour des installations - Saint Henri Basketball",
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
                var content = EmailTemplates.Season.GetSeasonStatusUpdateEmail(
                    user.FirstName,
                    season.Name,
                    season.Status.ToString(),
                    $"Changement de statut effectif à partir du {DateTime.UtcNow:dd MMMM yyyy}",
                    season.Notes
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
                var updateSubject = "Mise à jour des détails de la saison";
                var changes = string.Join(", ", changedProperties.Select(p => p.ToLower()));
                var updateDetails = $"Les détails suivants ont été mis à jour: {changes}. Période: {season.StartDate:dd MMMM yyyy} - {season.EndDate:dd MMMM yyyy}. Prix: {season.Price:C}. {season.Notes}";

                var content = EmailTemplates.Season.GetSeasonUpdateEmail(
                    user.FirstName,
                    season.Name,
                    updateSubject,
                    updateDetails,
                    $"https://sainthenribasketball.com/season/{season.Id}",
                    "Voir les détails de la saison"
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
            var content = EmailTemplates.Season.GetSeasonPaymentReminderEmail(
                userName,
                registration.Season.Name,
                registration.Season.Price,
                $"https://sainthenribasketball.com/season/{registration.SeasonId}/payment",
                GenerateReference("SEASON"),
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

    #region Custom Email
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
    #endregion
}