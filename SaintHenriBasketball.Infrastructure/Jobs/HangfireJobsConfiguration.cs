using Hangfire;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.Infrastructure.Jobs;

public class HangfireJobsConfiguration
{
    public static void ConfigureRecurringJobs()
    {
        // Configure attendance reminder jobs
        // Run every Wednesday, Thursday, and Friday at 12:00 PM
        RecurringJob.AddOrUpdate<IEmailAutomationService>(
            "schedule-attendance-reminders-wed",
            service => service.ScheduleAttendanceRemindersForUpcomingSessions(),
            Cron.Weekly(DayOfWeek.Wednesday, 12, 0));

        RecurringJob.AddOrUpdate<IEmailAutomationService>(
            "schedule-attendance-reminders-thu",
            service => service.ScheduleAttendanceRemindersForUpcomingSessions(),
            Cron.Weekly(DayOfWeek.Thursday, 12, 0));

        RecurringJob.AddOrUpdate<IEmailAutomationService>(
            "schedule-attendance-reminders-fri",
            service => service.ScheduleAttendanceRemindersForUpcomingSessions(),
            Cron.Weekly(DayOfWeek.Friday, 12, 0));

        // Payment reminder jobs
        RecurringJob.AddOrUpdate<IEmailAutomationService>(
            "schedule-payment-reminders",
            service => service.SchedulePaymentReminders(),
            Cron.Daily(3, 0)); // Run daily at 3:00 AM

        // Bulk payment reminders
        RecurringJob.AddOrUpdate<IEmailAutomationService>(
            "send-bulk-payment-reminders",
            service => service.SendBulkPaymentReminders(),
            Cron.Weekly(DayOfWeek.Monday, 10, 0));
    }
}
