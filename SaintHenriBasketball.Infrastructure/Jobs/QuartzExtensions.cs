using Microsoft.Extensions.DependencyInjection;
using Quartz;
using SaintHenriBasketball.Application.Helpers;

namespace SaintHenriBasketball.Infrastructure.Jobs;

public static class QuartzExtensions
{
    public static IServiceCollection AddQuartzJobs(this IServiceCollection services)
    {
        var montreal = SessionTimeHelper.MontrealTimeZone;

        services.AddQuartz(q =>
        {
            // All triggers run in Montreal time so DST shifts don't drift the schedule.

            var attendanceJobKey = new JobKey("AttendanceReminder");
            q.AddJob<AttendanceReminderJob>(opts => opts.WithIdentity(attendanceJobKey).StoreDurably());
            q.AddTrigger(opts => opts
                .ForJob(attendanceJobKey)
                .WithIdentity("AttendanceReminder-trigger")
                .WithDescription("Attendance reminders Thu/Fri/Sat at 10 AM ET")
                .WithCronSchedule("0 0 10 ? * THU,FRI,SAT", x => x.InTimeZone(montreal)));

            // Service-side `(today - sessionDate) % 2` modulus caps to 4 reminders.
            var paymentJobKey = new JobKey("PaymentReminder");
            q.AddJob<PaymentReminderJob>(opts => opts.WithIdentity(paymentJobKey).StoreDurably());
            q.AddTrigger(opts => opts
                .ForJob(paymentJobKey)
                .WithIdentity("PaymentReminder-trigger")
                .WithDescription("Payment reminders daily at 12 PM ET")
                .WithCronSchedule("0 0 12 * * ?", x => x.InTimeZone(montreal)));

            // Idempotent — players who self-billed earlier via QR check-in are skipped.
            var billingJobKey = new JobKey("DropInBilling");
            q.AddJob<DropInBillingJob>(opts => opts.WithIdentity(billingJobKey).StoreDurably());
            q.AddTrigger(opts => opts
                .ForJob(billingJobKey)
                .WithIdentity("DropInBilling-trigger")
                .WithDescription("Auto-bill drop-in players Sat 11 AM ET")
                .WithCronSchedule("0 0 11 ? * SAT", x => x.InTimeZone(montreal)));

            var capacityJobKey = new JobKey("CapacityCheck");
            q.AddJob<CapacityCheckJob>(opts => opts.WithIdentity(capacityJobKey).StoreDurably());
            q.AddTrigger(opts => opts
                .ForJob(capacityJobKey)
                .WithIdentity("CapacityCheck-morning-trigger")
                .WithDescription("Session capacity check at 9 AM ET")
                .WithCronSchedule("0 0 9 * * ?", x => x.InTimeZone(montreal)));
            q.AddTrigger(opts => opts
                .ForJob(capacityJobKey)
                .WithIdentity("CapacityCheck-evening-trigger")
                .WithDescription("Session capacity check at 6 PM ET")
                .WithCronSchedule("0 0 18 * * ?", x => x.InTimeZone(montreal)));

            // ScheduledEmailJob has no trigger — instances are scheduled dynamically by
            // EmailAutomationService for one-off sends.
            q.AddJob<ScheduledEmailJob>(opts => opts
                .WithIdentity("ScheduledEmail")
                .StoreDurably());

            // SmsReminderService short-circuits when the sms-reminders flag is off, so
            // hourly is safe regardless of feature flag state.
            var smsJobKey = new JobKey("SmsReminder");
            q.AddJob<SmsReminderJob>(opts => opts.WithIdentity(smsJobKey).StoreDurably());
            q.AddTrigger(opts => opts
                .ForJob(smsJobKey)
                .WithIdentity("SmsReminder-trigger")
                .WithDescription("SMS session-day reminders, hourly ET")
                .WithCronSchedule("0 0 * * * ?", x => x.InTimeZone(montreal)));

            // Service window is exclusive on the upper bound so each session matches
            // exactly one tick of the hourly cron.
            var oneDayJobKey = new JobKey("OneDayReminder");
            q.AddJob<OneDayReminderJob>(opts => opts.WithIdentity(oneDayJobKey).StoreDurably());
            q.AddTrigger(opts => opts
                .ForJob(oneDayJobKey)
                .WithIdentity("OneDayReminder-trigger")
                .WithDescription("Email reminder 24 hours before each session, hourly ET")
                .WithCronSchedule("0 0 * * * ?", x => x.InTimeZone(montreal)));
        });

        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }
}
