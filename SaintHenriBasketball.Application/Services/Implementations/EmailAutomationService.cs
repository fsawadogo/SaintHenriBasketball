using AutoMapper;
using Hangfire;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Pdf.Filters;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Enums;
using static SaintHenriBasketball.Application.Templates.EmailTemplates;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class EmailAutomationService : IEmailAutomationService
{
    private readonly ISessionService _sessionService;
    private readonly IPaymentService _paymentService;
    private readonly IUserService _userService;
    private readonly IAttendanceService _attendanceService;
    private readonly IEmailService _emailService;
    private readonly IMapper _mapper;
    private readonly ILogger<EmailAutomationService> _logger;

    public EmailAutomationService(
        ISessionService sessionService,
        IPaymentService paymentService,
        IUserService userService,
        IAttendanceService attendanceService,
        IEmailService emailService,
        IMapper mapper,
        ILogger<EmailAutomationService> logger)
    {
        _sessionService = sessionService;
        _paymentService = paymentService;
        _userService = userService;
        _attendanceService = attendanceService;
        _emailService = emailService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Schedules attendance reminders for upcoming sessions
    /// Only runs on Wednesday, Thursday, and Friday
    /// </summary>
    public async Task ScheduleAttendanceRemindersForUpcomingSessions()
    {
        try
        {
            // Get today's date in local time
            var today = DateTime.Now.Date;

            // Only run this on Wednesday, Thursday, or Friday
            if (today.DayOfWeek != DayOfWeek.Tuesday &&
                today.DayOfWeek != DayOfWeek.Thursday &&
                today.DayOfWeek != DayOfWeek.Friday)
            {
                _logger.LogInformation("Skipping attendance reminders schedule - today is not Wednesday, Thursday or Friday");
                return;
            }

            // Get the upcoming sessions
            var upcomingSessions = await _sessionService.GetUpcomingSessionsAsync();

            // Filter sessions that are 1 - 3 days ahead(based on today)
        // Ensure we're comparing dates with the same timezone basis
        var localToday = DateTime.Now.Date;
            var sessionsToRemind = upcomingSessions.Where(session => {
                // Convert session date to local time for comparison if needed
                var sessionLocalDate = TimeZoneInfo.ConvertTimeFromUtc(
                    session.SessionDate.ToUniversalTime(),
                    TimeZoneInfo.Local).Date;

                var daysDifference = (session.SessionDate.Date - today).Days;
                return daysDifference >= 1 && daysDifference <= 6;
            }).ToList();

            if (!sessionsToRemind.Any())
            {
                _logger.LogInformation("No upcoming sessions to send reminders for in the next few days");
                return;
            }

            foreach (var session in sessionsToRemind)
            {
                // Schedule immediate reminders
                BackgroundJob.Enqueue(() => SendAttendanceRemindersForSession(session.Id));

                _logger.LogInformation("Scheduled attendance reminder for session {SessionId} on {SessionDate}",
                    session.Id, session.SessionDate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling attendance reminders");
        }
    }

    /// <summary>
    /// Sends attendance reminders for a specific session
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task SendAttendanceRemindersForSession(Guid sessionId)
    {
        try
        {
            // Get session details
            var session = await _sessionService.GetSessionAsync(sessionId);

            // Skip if session doesn't exist or is not open
            if (session == null || session.Status != SessionStatus.Open)
            {
                _logger.LogWarning("Cannot send reminders for session {SessionId}: Session not found or not open", sessionId);
                return;
            }

            // Get all attendance for this session
            var summary = await _attendanceService.GetSessionAttendanceSummaryAsync(sessionId);

            // Get all registered users for the session
            var attendees = await _attendanceService.GetSessionAttendeesAsync(sessionId);

            int successCount = 0;
            int failureCount = 0;
            var failedEmails = new List<string?>();

            foreach (var attendee in attendees)
            {
                try
                {
                    // Skip users who have already marked their attendance
                    if (summary.Attendances.Any(a => a.UserId == attendee.UserId))
                    {
                        _logger.LogInformation("User {UserId} already marked attendance for session {SessionId}",
                            attendee.UserId, sessionId);
                        continue;
                    }

                    // Get user details
                    var user = await _userService.GetUserAsync(attendee.UserId);

                    // Send reminder
                    await _emailService.SendAttendanceReminderEmailAsync(
                        attendee.UserId,
                        $"Ne manquez pas la session de basket de ce week-end! Veuillez nous faire savoir si vous pouvez y assister."
                    );

                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send attendance reminder to user {UserId}", attendee.UserId);
                    failureCount++;
                    failedEmails.Add(attendee.Email);
                }
            }

            _logger.LogInformation(
                "Sent {SuccessCount} attendance reminders for session {SessionId}, {FailureCount} failed",
                successCount, sessionId, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending attendance reminders for session {SessionId}", sessionId);
            throw; // Allow Hangfire to retry
        }
    }

    /// <summary>
    /// Schedules payment reminders for pending payments
    /// </summary>
    public async Task SchedulePaymentReminders()
    {
        try
        {
            // Get all payments
            var allPayments = await _paymentService.GetAllPayments();

            // Filter to find pending payments
            var pendingPayments = allPayments.Where(p => p.Status == PaymentStatus.Pending).ToList();

            foreach (var payment in pendingPayments)
            {
                // Calculate when to send the reminder
                // If payment is older than 3 days, schedule for immediate sending
                // Otherwise schedule for 3 days after creation
                var paymentDate = payment.PaymentDate;
                var reminderDate = paymentDate.AddDays(3);
                var now = DateTime.UtcNow;

                if (reminderDate <= now)
                {
                    // Schedule immediately for payments older than 3 days
                    BackgroundJob.Enqueue(() => SendPaymentReminderForUser(payment.UserId, payment.Id));
                    _logger.LogInformation("Scheduled immediate payment reminder for user {UserId}, payment {PaymentId}",
                        payment.UserId, payment.Id);
                }
                else
                {
                    // Schedule for future for newer payments
                    BackgroundJob.Schedule(() => SendPaymentReminderForUser(payment.UserId, payment.Id),
                        reminderDate - now);
                    _logger.LogInformation("Scheduled payment reminder for user {UserId}, payment {PaymentId} at {ReminderTime}",
                        payment.UserId, payment.Id, reminderDate);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling payment reminders");
        }
    }

    /// <summary>
    /// Sends a payment reminder to a specific user
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task SendPaymentReminderForUser(Guid userId, Guid paymentId)
    {
        try
        {
            // Get payment details
            var payment = await _paymentService.GetPaymentAsync(paymentId);
            if (payment == null || payment.Status != PaymentStatus.Pending)
            {
                _logger.LogInformation("Skipping payment reminder: Payment {PaymentId} not found or not pending", paymentId);
                return;
            }

            // Get user details
            var user = await _userService.GetUserAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Cannot send payment reminder: User {UserId} not found", userId);
                return;
            }

            // Using the properly styled payment reminder email with Stripe payment link
            await _emailService.SendPaymentReminderEmailAsync(
                payment.UserEmail,
                payment.UserName,
                user.PaymentPlan,
                $"Votre paiement de ${payment.Amount} du {payment.PaymentDate:dd MMMM yyyy} est toujours en attente. " +
                $"Vous pouvez facilement effectuer votre paiement en utilisant nos options de paiement sécurisées ci-dessous."
            );

            _logger.LogInformation("Sent payment reminder to user {UserId} for payment {PaymentId}", userId, paymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending payment reminder to user {UserId} for payment {PaymentId}", userId, paymentId);
            throw; // Allow Hangfire to retry
        }
    }

    /// <summary>
    /// Sends bulk payment reminders to all users with pending payments
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task SendBulkPaymentReminders()
    {
        try
        {
            // Get all pending payments
            var allPayments = await _paymentService.GetAllPayments();
            var pendingPayments = allPayments.Where(p => p.Status == PaymentStatus.Pending).ToList();

            if (!pendingPayments.Any())
            {
                _logger.LogInformation("No pending payments found for bulk reminders");
                return;
            }

            int successCount = 0;
            int failureCount = 0;
            var failedEmails = new List<string?>();

            foreach (var payment in pendingPayments)
            {
                try
                {
                    if (string.IsNullOrEmpty(payment.UserEmail))
                        continue;

                    // Get user to determine payment plan
                    var user = await _userService.GetUserAsync(payment.UserId);
                    if (user == null)
                        continue;

                    await _emailService.SendPaymentReminderEmailAsync(
                        payment.UserEmail,
                        payment.UserName,
                        user.PaymentPlan,
                        $"Votre paiement de ${payment.Amount} est toujours en attente. " +
                        "Veuillez utiliser l'une des méthodes de paiement ci-dessous pour finaliser votre paiement."
                    );

                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send payment reminder to {Email}", payment.UserEmail);
                    failureCount++;
                    failedEmails.Add(payment.UserEmail);
                }
            }

            _logger.LogInformation(
                "Sent {SuccessCount} bulk payment reminders, {FailureCount} failed",
                successCount, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk payment reminders");
            throw; // Allow Hangfire to retry
        }
    }

    /// <summary>
    /// Sends session cancellation notifications to all registered users
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task SendSessionCancellationNotifications(Guid sessionId, string? cancellationReason = null)
    {
        try
        {
            // Get session details
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null)
            {
                _logger.LogWarning("Cannot send cancellation notification: Session {SessionId} not found", sessionId);
                return;
            }

            // Get all attendees for this session
            var attendees = await _attendanceService.GetSessionAttendeesAsync(sessionId);
            if (!attendees.Any())
            {
                _logger.LogInformation("No users registered for cancelled session {SessionId}", sessionId);
                return;
            }

            int successCount = 0;
            int failureCount = 0;
            var failedEmails = new List<string?>();

            foreach (var attendee in attendees)
            {
                try
                {
                    if (string.IsNullOrEmpty(attendee.Email))
                        continue;

                    var sessionDate = session.SessionDate.ToString("dddd, MMMM d");
                    var message = $"Nous regrettons de vous informer que la séance de basketball prévue le {sessionDate} a été annulée.";

                    if (!string.IsNullOrEmpty(cancellationReason))
                    {
                        message += $" Raison: {cancellationReason}";
                    }

                    message += " Nous nous excusons pour tout inconvénient causé.";

                    // Use the EmailTemplates for consistent styling 
                    await _emailService.SendGeneralAnnouncementEmailAsync(
                        attendee.Email,
                        $"{attendee.FirstName} {attendee.LastName}",
                        message
                    );

                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send cancellation notification to user {UserId}", attendee.UserId);
                    failureCount++;
                    failedEmails.Add(attendee.Email);
                }
            }

            _logger.LogInformation(
                "Sent {SuccessCount} cancellation notifications for session {SessionId}, {FailureCount} failed",
                successCount, sessionId, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending cancellation notifications for session {SessionId}", sessionId);
            throw; // Allow Hangfire to retry
        }
    }
}
