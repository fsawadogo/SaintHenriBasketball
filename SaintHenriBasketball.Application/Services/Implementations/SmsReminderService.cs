using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SmsReminderService : ISmsReminderService
{
    // Window is `[now+lead-half, now+lead+half)`. Upper bound exclusive so an hourly
    // cron never double-messages a session whose start falls on the boundary.
    private static readonly TimeSpan ReminderLeadTime = TimeSpan.FromHours(2);
    private static readonly TimeSpan ReminderWindowSize = TimeSpan.FromHours(1);

    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionRegistrationRepository _registrationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISmsService _smsService;
    private readonly IFeatureFlagService _featureFlagService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SmsReminderService> _logger;

    public SmsReminderService(
        ISessionRepository sessionRepository,
        ISessionRegistrationRepository registrationRepository,
        IUserRepository userRepository,
        ISmsService smsService,
        IFeatureFlagService featureFlagService,
        INotificationService notificationService,
        ILogger<SmsReminderService> logger)
    {
        _sessionRepository = sessionRepository;
        _registrationRepository = registrationRepository;
        _userRepository = userRepository;
        _smsService = smsService;
        _featureFlagService = featureFlagService;
        _notificationService = notificationService;
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
            .Where(s => IsWithinWindow(s.SessionDate, s.StartTime, lower, upper))
            .ToList();

        if (candidates.Count == 0) return 0;

        var sent = 0;
        foreach (var session in candidates)
        {
            var registrations = await _registrationRepository.GetBySessionIdAsync(session.Id);
            if (registrations.Count == 0) continue;

            var missingUserIds = registrations
                .Where(r => r.User is null)
                .Select(r => r.UserId)
                .Distinct()
                .ToList();
            var fetched = missingUserIds.Count > 0
                ? (await _userRepository.GetUsersByIdsAsync(missingUserIds)).ToDictionary(u => u.Id)
                : new();

            var displayTime = SessionTimeHelper.FormatDisplay(session.StartTime);

            foreach (var reg in registrations)
            {
                var user = reg.User ?? (fetched.TryGetValue(reg.UserId, out var u) ? u : null);
                if (user is null) continue;

                await _notificationService.CreateAsync(
                    user.Id,
                    Domain.Entities.NotificationType.SessionReminder,
                    title: EmailTemplateHelper.L("Session in 2 hours", "Séance dans 2 heures", user.PreferredLanguage),
                    body: EmailTemplateHelper.L(
                        $"Your session at {displayTime} starts soon. See you at {session.Location}.",
                        $"Votre séance à {displayTime} commence bientôt. À tantôt au {session.Location}.",
                        user.PreferredLanguage),
                    url: "/dashboard");

                if (!CanReceiveSms(user)) continue;

                var message = EmailTemplateHelper.L(
                    $"SHB reminder: your session is today at {displayTime}. See you at {session.Location}.",
                    $"Rappel SHB: votre séance est aujourd'hui à {displayTime}. À tantôt au {session.Location}.",
                    user.PreferredLanguage);

                if (await _smsService.SendAsync(user.PhoneNumber!, message))
                    sent++;
            }
        }

        _logger.LogInformation("SmsReminder run: {Sessions} sessions in window, {Sent} messages sent", candidates.Count, sent);
        return sent;
    }

    private static bool CanReceiveSms(Domain.Entities.ApplicationUser user) =>
        user.SmsOptIn && !string.IsNullOrEmpty(user.PhoneNumber);

    private static bool IsWithinWindow(DateTime sessionDate, string? startTime, DateTime lowerUtc, DateTime upperUtc)
    {
        var local = SessionTimeHelper.CombineLocal(sessionDate, startTime);
        var utc = SessionTimeHelper.ToUtc(local);
        return utc >= lowerUtc && utc < upperUtc;
    }
}
