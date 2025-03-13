using Hangfire;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.Infrastructure.Jobs;

public class HangfireJobsConfiguration
{
    public static void ConfigureRecurringJobs()
    {
        // Configure attendance reminder jobs
        // Run every Wednesday, Thursday, and Friday at 13:00 PM
        RecurringJob.AddOrUpdate<IEmailAutomationService>(
            "schedule-attendance-reminders-wed",
            service => service.ScheduleAttendanceRemindersForUpcomingSessions(),
            Cron.Weekly(DayOfWeek.Wednesday, 13, 0));

        RecurringJob.AddOrUpdate<IEmailAutomationService>(
            "schedule-attendance-reminders-thu",
            service => service.ScheduleAttendanceRemindersForUpcomingSessions(),
            Cron.Weekly(DayOfWeek.Thursday, 13, 0));

        RecurringJob.AddOrUpdate<IEmailAutomationService>(
            "schedule-attendance-reminders-fri",
            service => service.ScheduleAttendanceRemindersForUpcomingSessions(),
            Cron.Weekly(DayOfWeek.Friday, 13, 0));

        // Payment reminder jobs
        RecurringJob.AddOrUpdate<IEmailAutomationService>(
            "schedule-payment-reminders",
            service => service.SchedulePaymentReminders(),
            Cron.Daily(17, 30));

        // Check session capacities and send reminders if needed
        // Run twice daily at 9:00 AM and 6:00 PM
        RecurringJob.AddOrUpdate<IEmailAutomationService>(
            "check-session-capacities-morning",
            service => service.CheckSessionsCapacityAndSendReminders(),
            Cron.Daily(9, 0));

        RecurringJob.AddOrUpdate<IEmailAutomationService>(
            "check-session-capacities-evening",
            service => service.CheckSessionsCapacityAndSendReminders(),
            Cron.Daily(18, 0));
    }
}
