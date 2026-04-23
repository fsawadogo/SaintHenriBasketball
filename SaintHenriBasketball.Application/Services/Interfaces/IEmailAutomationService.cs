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

    /// <summary>
    /// Sends reminder emails to users who haven't confirmed their attendance
    /// when the capacity reaches specified thresholds (75% and 85%)
    /// </summary>
    Task SendCapacityThresholdRemindersAsync(Guid sessionId);

    /// <summary>
    /// Checks all upcoming sessions and triggers capacity threshold reminders if needed
    /// </summary>
    Task CheckSessionsCapacityAndSendReminders();

    /// <summary>
    /// Sends a 24-hour-ahead reminder to every registered user of any session
    /// starting in [now+23.5h, now+24.5h). Run hourly by Quartz; the exclusive
    /// upper bound prevents double-sends across consecutive ticks.
    /// </summary>
    Task SendOneDayAheadRemindersAsync();
}
