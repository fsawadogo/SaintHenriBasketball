namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IEmailAutomationService
{
    /// <summary>
    /// Schedules attendance reminders for upcoming sessions
    /// </summary>
    Task ScheduleAttendanceRemindersForUpcomingSessions();

    /// <summary>
    /// Sends attendance reminders for a specific session
    /// </summary>
    Task SendAttendanceRemindersForSession(Guid sessionId);

    /// <summary>
    /// Schedules payment reminders for pending payments
    /// </summary>
    Task SchedulePaymentReminders();

    /// <summary>
    /// Sends a payment reminder to a specific user
    /// </summary>
    Task SendPaymentReminderForUser(Guid userId, Guid paymentId);

    /// <summary>
    /// Sends bulk payment reminders to all users with pending payments
    /// </summary>
    Task SendBulkPaymentReminders();

    /// <summary>
    /// Sends session cancellation notifications to all registered users
    /// </summary>
    Task SendSessionCancellationNotifications(Guid sessionId, string? cancellationReason = null);
}
