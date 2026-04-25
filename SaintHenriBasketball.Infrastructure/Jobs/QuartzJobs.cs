using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.Infrastructure.Jobs;

/// <summary>Base class for recurring jobs that call IEmailAutomationService methods.</summary>
[DisallowConcurrentExecution]
public abstract class EmailAutomationJob : IJob
{
    private readonly IServiceProvider _sp;
    private readonly ILogger _logger;

    protected EmailAutomationJob(IServiceProvider sp, ILogger logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected abstract string JobName { get; }
    protected abstract Task RunAsync(IEmailAutomationService svc);

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Running {JobName}", JobName);
        using var scope = _sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEmailAutomationService>();
        await RunAsync(svc);
    }
}

public class AttendanceReminderJob(IServiceProvider sp, ILogger<AttendanceReminderJob> logger) : EmailAutomationJob(sp, logger)
{
    protected override string JobName => "AttendanceReminder";
    protected override Task RunAsync(IEmailAutomationService svc) => svc.ScheduleAttendanceRemindersForUpcomingSessions();
}

public class PaymentReminderJob(IServiceProvider sp, ILogger<PaymentReminderJob> logger) : EmailAutomationJob(sp, logger)
{
    protected override string JobName => "PaymentReminder";
    protected override Task RunAsync(IEmailAutomationService svc) => svc.SchedulePaymentReminders();
}

public class CapacityCheckJob(IServiceProvider sp, ILogger<CapacityCheckJob> logger) : EmailAutomationJob(sp, logger)
{
    protected override string JobName => "CapacityCheck";
    protected override Task RunAsync(IEmailAutomationService svc) => svc.CheckSessionsCapacityAndSendReminders();
}

public class OneDayReminderJob(IServiceProvider sp, ILogger<OneDayReminderJob> logger) : EmailAutomationJob(sp, logger)
{
    protected override string JobName => "OneDayReminder";
    protected override Task RunAsync(IEmailAutomationService svc) => svc.SendOneDayAheadRemindersAsync();
}

/// <summary>One-off job for sending scheduled emails.</summary>
[DisallowConcurrentExecution]
public class ScheduledEmailJob : IJob
{
    private readonly IServiceProvider _sp;

    public ScheduledEmailJob(IServiceProvider sp) => _sp = sp;

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var to = data.GetString("to") ?? throw new InvalidOperationException("Missing 'to' in job data");
        var subject = data.GetString("subject") ?? throw new InvalidOperationException("Missing 'subject' in job data");
        var body = data.GetString("body") ?? "";

        using var scope = _sp.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        await emailService.SendEmailAsync(to, subject, body);
    }
}

[DisallowConcurrentExecution]
public class SmsReminderJob : IJob
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<SmsReminderJob> _logger;

    public SmsReminderJob(IServiceProvider sp, ILogger<SmsReminderJob> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Running SmsReminder");
        using var scope = _sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISmsReminderService>();
        await svc.SendDueRemindersAsync();
    }
}

[DisallowConcurrentExecution]
public class DropInBillingJob : IJob
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<DropInBillingJob> _logger;

    public DropInBillingJob(IServiceProvider sp, ILogger<DropInBillingJob> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Running DropInBilling");
        using var scope = _sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var created = await svc.RunDailyDropInBillingAsync();
        _logger.LogInformation("DropInBilling created {Count} payments", created);
    }
}
