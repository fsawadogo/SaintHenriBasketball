using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class AttendanceReminderJob : IJob
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<AttendanceReminderJob> _logger;

    public AttendanceReminderJob(IServiceProvider sp, ILogger<AttendanceReminderJob> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Running attendance reminder job");
        using var scope = _sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEmailAutomationService>();
        await svc.ScheduleAttendanceRemindersForUpcomingSessions();
    }
}

[DisallowConcurrentExecution]
public class PaymentReminderJob : IJob
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<PaymentReminderJob> _logger;

    public PaymentReminderJob(IServiceProvider sp, ILogger<PaymentReminderJob> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Running payment reminder job");
        using var scope = _sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEmailAutomationService>();
        await svc.SchedulePaymentReminders();
    }
}

[DisallowConcurrentExecution]
public class CapacityCheckJob : IJob
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<CapacityCheckJob> _logger;

    public CapacityCheckJob(IServiceProvider sp, ILogger<CapacityCheckJob> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Running capacity check job");
        using var scope = _sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEmailAutomationService>();
        await svc.CheckSessionsCapacityAndSendReminders();
    }
}

/// <summary>One-off job for sending scheduled emails</summary>
public class ScheduledEmailJob : IJob
{
    private readonly IServiceProvider _sp;

    public ScheduledEmailJob(IServiceProvider sp) => _sp = sp;

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var to = data.GetString("to") ?? "";
        var subject = data.GetString("subject") ?? "";
        var body = data.GetString("body") ?? "";

        using var scope = _sp.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        await emailService.SendEmailAsync(to, subject, body);
    }
}
