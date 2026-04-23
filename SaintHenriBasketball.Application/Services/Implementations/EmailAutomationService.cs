using AutoMapper;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Users;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class EmailAutomationService : IEmailAutomationService
{
    private readonly ISessionService _sessionService;
    private readonly IPaymentService _paymentService;
    private readonly IUserService _userService;
    private readonly IAttendanceService _attendanceService;
    private readonly IEmailService _emailService;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<EmailAutomationService> _logger;

    public EmailAutomationService(
        ISessionService sessionService,
        IPaymentService paymentService,
        IUserService userService,
        IAttendanceService attendanceService,
        IEmailService emailService,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<EmailAutomationService> logger)
    {
        _sessionService = sessionService;
        _paymentService = paymentService;
        _userService = userService;
        _attendanceService = attendanceService;
        _emailService = emailService;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task SendOneDayAheadRemindersAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var lower = now.AddHours(23.5);
            var upper = now.AddHours(24.5);

            var upcoming = await _sessionService.GetUpcomingSessionsAsync();
            var due = upcoming
                .Where(s => s.Status == SessionStatus.Open)
                .Where(s =>
                {
                    // Sessions are stored with date-only; combine with StartTime for an accurate UTC instant.
                    var startUtc = Helpers.SessionTimeHelper.ToUtc(
                        Helpers.SessionTimeHelper.CombineLocal(s.SessionDate, s.StartTime));
                    return startUtc >= lower && startUtc < upper;
                })
                .ToList();

            if (due.Count == 0)
            {
                _logger.LogInformation("OneDayAheadReminders: no sessions in the [now+23.5h, now+24.5h) window");
                return;
            }

            _logger.LogInformation("OneDayAheadReminders: sending for {Count} session(s)", due.Count);
            foreach (var session in due)
            {
                await SendAttendanceRemindersForSession(session.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OneDayAheadReminders run failed");
        }
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
            var today = DateTime.UtcNow.Date;

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
            var localToday = DateTime.UtcNow.Date;
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
                await SendAttendanceRemindersForSession(session.Id);

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
            throw;
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
                    await SendPaymentReminderForUser(payment.UserId, payment.Id);
                    _logger.LogInformation("Scheduled immediate payment reminder for user {UserId}, payment {PaymentId}",
                        payment.UserId, payment.Id);
                }
                else
                {
                    // Schedule for future for newer payments
                    // Will be sent on next scheduled run when reminderDate has passed
                    _logger.LogInformation("Payment reminder for user {UserId} will be sent after {ReminderDate}", payment.UserId, reminderDate);
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

            // Custom message with payment details
            string customMessage = $"Votre paiement de {payment.Amount:C} du {payment.PaymentDate:dd MMMM yyyy} est toujours en attente. " +
                "Vous pouvez facilement effectuer votre paiement en utilisant nos options de paiement sécurisées ci-dessous.";

            // Using the properly styled payment reminder email with Stripe payment link
            await _emailService.SendPaymentReminderEmailAsync(
                payment.UserId,
                user.PaymentPlan,
                customMessage
            );

            _logger.LogInformation("Sent payment reminder to user {UserId} for payment {PaymentId}", userId, paymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending payment reminder to user {UserId} for payment {PaymentId}", userId, paymentId);
            throw;
        }
    }

    /// <summary>
    /// Sends bulk payment reminders to all users with pending payments
    /// </summary>
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

            // Collect emails for bulk sending
            var userEmails = new List<string>();
            var customMessage = "Un rappel amical concernant votre paiement en attente. " +
                "Veuillez utiliser l'une des méthodes de paiement ci-dessous pour finaliser votre transaction.";

            foreach (var payment in pendingPayments)
            {
                // Skip null emails
                if (string.IsNullOrEmpty(payment.UserEmail))
                    continue;

                userEmails.Add(payment.UserEmail);
            }

            // Send bulk reminders
            if (userEmails.Any())
            {
                var result = await _emailService.SendPaymentRemindersAsync(
                    userEmails,
                    EmailLanguage.French,
                    customMessage
                );

                _logger.LogInformation(
                    "Sent {SuccessCount} bulk payment reminders, {FailureCount} failed",
                    result.SuccessCount, result.FailureCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk payment reminders");
            throw;
        }
    }

    /// <summary>
    /// Sends session cancellation notifications to all registered users
    /// </summary>
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
            throw;
        }
    }
    /// <summary>
    /// Checks for sessions with low attendance and sends warnings
    /// </summary>
    public async Task CheckLowAttendanceSessions()
    {
        try
        {
            // Get upcoming sessions in the next 48 hours
            var upcomingSessions = await _sessionService.GetUpcomingSessionsAsync();
            var soonSessions = upcomingSessions.Where(s =>
                (s.SessionDate - DateTime.UtcNow).TotalHours <= 48 &&
                s.Status == SessionStatus.Open).ToList();

            foreach (var session in soonSessions)
            {
                // Get attendance summary
                var summary = await _attendanceService.GetSessionAttendanceSummaryAsync(session.Id);
                var confirmedAttendees = summary.Attendances.Count(a => a.IsAttending);

                // Check if attendance is below minimum
                if (confirmedAttendees < 3)
                {
                    // Get all attendees
                    var attendees = await _attendanceService.GetSessionAttendeesAsync(session.Id);
                    var users = new List<UserDto>();

                    foreach (var attendee in attendees)
                    {
                        var user = await _userService.GetUserAsync(attendee.UserId);
                        if (user != null)
                        {
                            users.Add(user);
                        }
                    }

                    if (users.Any())
                    {
                        await _emailService.SendLowAttendanceWarningEmailAsync(session, users);
                        _logger.LogInformation(
                            "Sent low attendance warnings for session {SessionId} ({Date}) - {Confirmed}/{Required} confirmed",
                            session.Id, session.SessionDate, confirmedAttendees, 3);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for low attendance sessions");
            throw;
        }
    }

    /// <summary>
    /// Sends reminder emails to users who haven't confirmed their attendance
    /// when the capacity reaches specified thresholds (75% and 85%)
    /// </summary>
    public async Task SendCapacityThresholdRemindersAsync(Guid sessionId)
    {
        try
        {
            // Get session details
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null || session.Status != SessionStatus.Open)
            {
                _logger.LogWarning($"Cannot send capacity reminders for session {sessionId}: Session not found or not open", sessionId);
                return;
            }

            // Calculate capacity and thresholds
            int totalCapacity = session.MaxCapacity;
            int spotsReserved = session.RegisteredPlayersCount;
            int spotsRemaining = totalCapacity - spotsReserved;
            int spotsRemainingPercentage = (int)((spotsRemaining / (double)totalCapacity) * 100);

            // Check if we're at one of our threshold points (25% spots remaining = 75% full, or 15% spots remaining = 85% full)
            bool isAt75PercentCapacity = spotsRemainingPercentage <= 25 && spotsRemainingPercentage > 15;
            bool isAt85PercentCapacity = spotsRemainingPercentage <= 15;

            // If we're not at a threshold, exit early
            if (!isAt75PercentCapacity && !isAt85PercentCapacity)
            {
                _logger.LogInformation(
                    $"Session {sessionId} capacity ({spotsRemaining}/{totalCapacity}) not at a notification threshold",
                    sessionId, spotsRemaining, totalCapacity);
                return;
            }

            // Get attendance information
            var attendanceSummary = await _attendanceService.GetSessionAttendanceSummaryAsync(sessionId);

            // Get all registered users for this session
            var registeredUsers = await _attendanceService.GetSessionAttendeesAsync(sessionId);

            // Filter users who haven't confirmed attendance yet
            var unconfirmedUsers = registeredUsers
                .Where(user => !attendanceSummary.Attendances.Any(a => a.UserId == user.UserId))
                .ToList();

            if (!unconfirmedUsers.Any())
            {
                _logger.LogInformation("No unconfirmed users for session {SessionId}", sessionId);
                return;
            }

            // Track email send results
            int successCount = 0;
            int failureCount = 0;
            var failedEmails = new List<string>();

            // Determine the message based on threshold
            string urgencyLevel = isAt85PercentCapacity ? "URGENT" : "Important";
            string spotsMessage = $"Il ne reste que {spotsRemaining} places!";

            foreach (var user in unconfirmedUsers)
            {
                try
                {
                    if (string.IsNullOrEmpty(user.Email))
                        continue;

                    // Send capacity threshold email
                    string message = $"{urgencyLevel}: {spotsMessage} Veuillez confirmer votre présence pour la prochaine session de basketball le {session.SessionDate:dddd, MMMM d} à 10:00. " +
                 "Si vous ne pouvez pas y assister, veuillez nous en informer afin que d'autres puissent se joindre.";


                    await _emailService.SendAttendanceReminderEmailAsync(
                        user.UserId,
                        message
                    );

                    successCount++;

                    // Add a small delay to avoid overloading email service
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send capacity threshold email to user {UserId}", user.UserId);
                    failureCount++;
                    failedEmails.Add(user.Email);
                }
            }

            _logger.LogInformation(
                $"Sent {successCount} capacity threshold ({spotsRemainingPercentage}%) notifications for session {sessionId}, {failureCount} failed",
                successCount,
                isAt85PercentCapacity ? 85 : 75,
                sessionId,
                failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending capacity threshold notifications for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Checks all upcoming sessions and triggers capacity threshold reminders if needed
    /// </summary>
    public async Task CheckSessionsCapacityAndSendReminders()
    {
        try
        {
            // Get upcoming sessions
            var upcomingSessions = await _sessionService.GetUpcomingSessionsAsync();

            foreach (var session in upcomingSessions)
            {
                // Calculate capacity and thresholds
                int totalCapacity = session.MaxCapacity;
                int spotsReserved = session.RegisteredPlayersCount;
                int spotsRemaining = totalCapacity - spotsReserved;
                int spotsRemainingPercentage = (int)((spotsRemaining / (double)totalCapacity) * 100);

                // Check if we're at one of our threshold points
                bool isAt75PercentCapacity = spotsRemainingPercentage <= 25 && spotsRemainingPercentage > 15;
                bool isAt85PercentCapacity = spotsRemainingPercentage <= 15;

                if (isAt75PercentCapacity || isAt85PercentCapacity)
                {
                    // Check if we've already sent notifications for this threshold
                    string cacheKey = $"CapacityNotification:{session.Id}:{(isAt85PercentCapacity ? 85 : 75)}";
                    var alreadySent = await _cacheService.GetAsync<bool>(cacheKey);

                    if (alreadySent)
                    {
                        _logger.LogInformation(
                            "Capacity notifications already sent for session {SessionId} at {Threshold}% threshold",
                            session.Id,
                            isAt85PercentCapacity ? 85 : 75);
                        continue;
                    }

                    // Schedule notification job
                    await SendCapacityThresholdRemindersAsync(session.Id);

                    // Mark this threshold as processed to avoid duplicate notifications
                    await _cacheService.SetAsync(cacheKey, true, TimeSpan.FromDays(7));

                    _logger.LogInformation(
                        "Scheduled capacity notifications for session {SessionId} at {Threshold}% threshold",
                        session.Id,
                        isAt85PercentCapacity ? 85 : 75);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking sessions capacity for threshold notifications");
        }
    }
}