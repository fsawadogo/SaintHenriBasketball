using System.Globalization;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SmsReminderService : ISmsReminderService
{
    // Sessions starting within this window (from the job's run time) are eligible.
    // Quartz fires hourly, so a 1-hour window centered at the 2h-ahead mark covers each session once.
    private static readonly TimeSpan ReminderLeadTime = TimeSpan.FromHours(2);
    private static readonly TimeSpan ReminderWindowSize = TimeSpan.FromHours(1);

    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionRegistrationRepository _registrationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISmsService _smsService;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<SmsReminderService> _logger;

    public SmsReminderService(
        ISessionRepository sessionRepository,
        ISessionRegistrationRepository registrationRepository,
        IUserRepository userRepository,
        ISmsService smsService,
        IFeatureFlagService featureFlagService,
        ILogger<SmsReminderService> logger)
    {
        _sessionRepository = sessionRepository;
        _registrationRepository = registrationRepository;
        _userRepository = userRepository;
        _smsService = smsService;
        _featureFlagService = featureFlagService;
        _logger = logger;
    }

    public async Task<int> SendDueRemindersAsync()
    {
        if (!await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.SmsReminders))
        {
            _logger.LogInformation("SMS reminders flag is off; skipping run");
            return 0;
        }

        var now = DateTime.UtcNow;
        var lower = now.Add(ReminderLeadTime - ReminderWindowSize / 2);
        var upper = now.Add(ReminderLeadTime + ReminderWindowSize / 2);

        var upcoming = await _sessionRepository.GetUpcomingSessionsAsync();
        var candidates = upcoming
            .Where(s => s.Status == SessionStatus.Open)
            .Where(s => IsWithinWindow(s.SessionDate, s.StartTime, lower, upper))
            .ToList();

        if (candidates.Count == 0) return 0;

        var sent = 0;
        foreach (var session in candidates)
        {
            var registrations = await _registrationRepository.GetBySessionIdAsync(session.Id);
            foreach (var reg in registrations)
            {
                var user = reg.User ?? await _userRepository.GetByIdAsync(reg.UserId);
                if (user is null || !user.SmsOptIn || string.IsNullOrEmpty(user.PhoneNumber)) continue;

                var message = EmailTemplateHelper.L(
                    $"SHB reminder: your session is today at {session.StartTime}. See you at {session.Location}.",
                    $"Rappel SHB: votre séance est aujourd'hui à {session.StartTime}. À tantôt au {session.Location}.",
                    user.PreferredLanguage);

                if (await _smsService.SendAsync(user.PhoneNumber, message))
                    sent++;
            }
        }

        _logger.LogInformation("SmsReminder run: {Sessions} sessions in window, {Sent} messages sent", candidates.Count, sent);
        return sent;
    }

    private static bool IsWithinWindow(DateTime sessionDate, string? startTime, DateTime lower, DateTime upper)
    {
        if (!TimeSpan.TryParse(startTime, CultureInfo.InvariantCulture, out var ts)) return false;
        var sessionStart = DateTime.SpecifyKind(sessionDate.Date.Add(ts), DateTimeKind.Utc);
        return sessionStart >= lower && sessionStart <= upper;
    }
}
