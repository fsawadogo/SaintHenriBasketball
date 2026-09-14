using System.Net;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Broadcast;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
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
    private readonly INotificationService _notificationService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly UnsubscribeLinks _unsubscribeLinks;
    private readonly ILogger<BroadcastService> _logger;

    public BroadcastService(
        IUserRepository userRepository,
        ISessionRegistrationRepository registrationRepository,
        ISessionAttendanceRepository attendanceRepository,
        IEmailService emailService,
        INotificationService notificationService,
        IAuditLogRepository auditLogRepository,
        UnsubscribeLinks unsubscribeLinks,
        ILogger<BroadcastService> logger)
    {
        _userRepository = userRepository;
        _registrationRepository = registrationRepository;
        _attendanceRepository = attendanceRepository;
        _emailService = emailService;
        _notificationService = notificationService;
        _auditLogRepository = auditLogRepository;
        _unsubscribeLinks = unsubscribeLinks;
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

    public async Task<SendBroadcastResultDto> SendAsync(SendBroadcastRequestDto request, Guid? adminId, string adminName)
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
            var french = user.PreferredLanguage == EmailLanguage.French;
            var subject = french && !string.IsNullOrWhiteSpace(request.SubjectFr) ? request.SubjectFr! : request.Subject;
            var body = french && !string.IsNullOrWhiteSpace(request.BodyFr) ? request.BodyFr! : request.BodyEn;
            try
            {
                var html = BuildEmailHtml(subject, body, _unsubscribeLinks.CreateUrl(user.Id), user.PreferredLanguage);
                await _emailService.SendEmailAsync(user.Email, subject, html);
                result.Succeeded++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Broadcast send failed for {Email}", user.Email);
                result.Failed++;
            }

            // Audience filter upstream excludes EmailNotificationsEnabled=false users —
            // so opting out of email broadcasts also suppresses the in-app mirror.
            await _notificationService.CreateAsync(
                user.Id,
                Domain.Entities.NotificationType.AdminBroadcast,
                title: subject,
                body: body,
                url: null);
        }

        _logger.LogInformation(
            "Broadcast sent — audience {Audience}, attempted {Attempted}, succeeded {Succeeded}, failed {Failed}",
            request.Audience, result.Attempted, result.Succeeded, result.Failed);

        await WriteAuditAsync(request, result, adminId, adminName);
        return result;
    }

    /// Turns the admin's plain-text body into escaped paragraphs inside the club email layout,
    /// keeping line breaks, and appends the unsubscribe footer CASL requires.
    public static string BuildEmailHtml(string subject, string body, string unsubscribeUrl, EmailLanguage language)
    {
        var paragraphs = body.Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(paragraph => EmailTemplateHelper.P(WebUtility.HtmlEncode(paragraph).Replace("\n", "<br/>")));

        var footer =
            "<p style='margin:24px 0 0;font-size:12px;line-height:1.5;color:#637369;'>" +
            EmailTemplateHelper.L(
                "You're receiving this because community updates are turned on in your SHB account.",
                "Vous recevez ce message parce que les nouvelles du club sont activées dans votre compte SHB.",
                language) +
            $" <a href='{WebUtility.HtmlEncode(unsubscribeUrl)}' style='color:{EmailTemplateHelper.ColorAccent};'>" +
            EmailTemplateHelper.L("Unsubscribe", "Se désabonner", language) +
            "</a></p>";

        return EmailTemplateHelper.BuildEmailLayout(subject, subject, string.Concat(paragraphs) + footer, language);
    }

    private async Task WriteAuditAsync(SendBroadcastRequestDto request, SendBroadcastResultDto result, Guid? adminId, string adminName)
    {
        try
        {
            var subject = request.Subject.Length > 100 ? request.Subject[..100] : request.Subject;
            await _auditLogRepository.AddAsync(new AuditLog(
                action: "Broadcast.Sent",
                entityType: "Broadcast",
                entityId: Guid.NewGuid(),
                details: $"Audience: {request.Audience}; subject: {subject}; attempted: {result.Attempted}; succeeded: {result.Succeeded}; failed: {result.Failed}",
                userId: adminId,
                userName: adminName));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit entry failed for broadcast to {Audience}", request.Audience);
        }
    }

    private async Task<IReadOnlyList<ApplicationUser>> ResolveAudienceAsync(BroadcastAudience audience)
    {
        // Honor per-user email opt-out for marketing/broadcast traffic. Transactional
        // emails (receipts, password reset, payment confirmation) bypass this and
        // continue to send — standard CASL/CAN-SPAM convention.
        var all = (await _userRepository.GetAllUsersAsync())
            .Where(u => u.EmailConfirmed && !string.IsNullOrEmpty(u.Email) && u.EmailNotificationsEnabled && u.CommunityUpdatesEnabled)
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
