using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace SaintHenriBasketball.Infrastructure.Jobs;

public static class QuartzExtensions
{
    public static IServiceCollection AddQuartzJobs(this IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            // Attendance reminders: Wed/Thu/Fri at 1:00 PM
            var attendanceJobKey = new JobKey("AttendanceReminder");
            q.AddJob<AttendanceReminderJob>(opts => opts.WithIdentity(attendanceJobKey).StoreDurably());
            q.AddTrigger(opts => opts
                .ForJob(attendanceJobKey)
                .WithIdentity("AttendanceReminder-trigger")
                .WithDescription("Attendance reminders Wed/Thu/Fri at 1 PM")
                .WithCronSchedule("0 0 13 ? * WED,THU,FRI"));

            // Payment reminders: Daily at 5:30 PM
            var paymentJobKey = new JobKey("PaymentReminder");
            q.AddJob<PaymentReminderJob>(opts => opts.WithIdentity(paymentJobKey).StoreDurably());
            q.AddTrigger(opts => opts
                .ForJob(paymentJobKey)
                .WithIdentity("PaymentReminder-trigger")
                .WithDescription("Payment reminders daily at 5:30 PM")
                .WithCronSchedule("0 30 17 * * ?"));

            // Capacity checks: Daily at 9 AM and 6 PM
            var capacityJobKey = new JobKey("CapacityCheck");
            q.AddJob<CapacityCheckJob>(opts => opts.WithIdentity(capacityJobKey).StoreDurably());
            q.AddTrigger(opts => opts
                .ForJob(capacityJobKey)
                .WithIdentity("CapacityCheck-morning-trigger")
                .WithDescription("Session capacity check at 9 AM")
                .WithCronSchedule("0 0 9 * * ?"));
            q.AddTrigger(opts => opts
                .ForJob(capacityJobKey)
                .WithIdentity("CapacityCheck-evening-trigger")
                .WithDescription("Session capacity check at 6 PM")
                .WithCronSchedule("0 0 18 * * ?"));

            // Register one-off job types (no triggers — scheduled dynamically)
            q.AddJob<ScheduledEmailJob>(opts => opts
                .WithIdentity("ScheduledEmail")
                .StoreDurably());
        });

        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }
}
