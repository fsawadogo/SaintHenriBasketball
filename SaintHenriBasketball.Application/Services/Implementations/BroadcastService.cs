using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Broadcast;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class BroadcastService : IBroadcastService
{
    private const int RecentNoShowLookbackDays = 30;
    private const double NoShowThreshold = 0.25;
    private const int PreviewSampleSize = 5;

    private readonly IUserRepository _userRepository;
    private readonly ISessionRegistrationRepository _registrationRepository;
    private readonly ISessionAttendanceRepository _attendanceRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<BroadcastService> _logger;

    public BroadcastService(
        IUserRepository userRepository,
        ISessionRegistrationRepository registrationRepository,
        ISessionAttendanceRepository attendanceRepository,
        IEmailService emailService,
        ILogger<BroadcastService> logger)
    {
        _userRepository = userRepository;
        _registrationRepository = registrationRepository;
        _attendanceRepository = attendanceRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<BroadcastAudiencePreviewDto> PreviewAudienceAsync(BroadcastAudience audience)
    {
        var recipients = await ResolveAudienceAsync(audience);
        return new BroadcastAudiencePreviewDto
        {
            RecipientCount = recipients.Count,
            SampleEmails = recipients
                .Where(u => !string.IsNullOrEmpty(u.Email))
                .Take(PreviewSampleSize)
                .Select(u => u.Email!)
                .ToList(),
        };
    }

    public async Task<SendBroadcastResultDto> SendAsync(SendBroadcastRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new ValidationException("Subject is required");
        if (string.IsNullOrWhiteSpace(request.BodyEn))
            throw new ValidationException("English body is required");

        var recipients = await ResolveAudienceAsync(request.Audience);
        var result = new SendBroadcastResultDto { Attempted = recipients.Count };

        foreach (var user in recipients)
        {
            if (string.IsNullOrEmpty(user.Email)) continue;
            var body = user.PreferredLanguage == EmailLanguage.French && !string.IsNullOrWhiteSpace(request.BodyFr)
                ? request.BodyFr
                : request.BodyEn;
            try
            {
                await _emailService.SendEmailAsync(user.Email, request.Subject, body);
                result.Succeeded++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Broadcast send failed for {Email}", user.Email);
                result.Failed++;
            }
        }

        _logger.LogInformation(
            "Broadcast sent — audience {Audience}, attempted {Attempted}, succeeded {Succeeded}, failed {Failed}",
            request.Audience, result.Attempted, result.Succeeded, result.Failed);

        return result;
    }

    private async Task<IReadOnlyList<ApplicationUser>> ResolveAudienceAsync(BroadcastAudience audience)
    {
        // Honor per-user email opt-out for marketing/broadcast traffic. Transactional
        // emails (receipts, password reset, payment confirmation) bypass this and
        // continue to send — standard CASL/CAN-SPAM convention.
        var all = (await _userRepository.GetAllUsersAsync())
            .Where(u => u.EmailConfirmed && !string.IsNullOrEmpty(u.Email) && u.EmailNotificationsEnabled)
            .ToList();

        return audience switch
        {
            BroadcastAudience.SeasonHolders => all.Where(u => u.PaymentPlan == PaymentPlan.Season).ToList(),
            BroadcastAudience.DropInOnly => all.Where(u => u.PaymentPlan == PaymentPlan.DropIn).ToList(),
            BroadcastAudience.RecentNoShows => await FilterRecentNoShowsAsync(all),
            _ => all,
        };
    }

    private async Task<IReadOnlyList<ApplicationUser>> FilterRecentNoShowsAsync(IReadOnlyList<ApplicationUser> candidates)
    {
        var cutoff = DateTime.UtcNow.AddDays(-RecentNoShowLookbackDays);
        var result = new List<ApplicationUser>();

        foreach (var user in candidates)
        {
            var registrations = await _registrationRepository.GetByUserIdInRangeAsync(user.Id, cutoff, DateTime.UtcNow);
            if (registrations.Count == 0) continue;

            var attendance = await _attendanceRepository.GetUserAttendanceHistoryAsync(user.Id);
            var attendedSessionIds = attendance
                .Where(a => a.IsAttending && a.Session is not null && a.Session.SessionDate >= cutoff)
                .Select(a => a.SessionId)
                .ToHashSet();

            var attended = registrations.Count(r => attendedSessionIds.Contains(r.SessionId));
            var rate = (double)attended / registrations.Count;
            if (rate < NoShowThreshold) result.Add(user);
        }

        return result;
    }
}
