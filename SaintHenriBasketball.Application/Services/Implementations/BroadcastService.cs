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
    private readonly BroadcastQueue _queue;
    private readonly ILogger<BroadcastService> _logger;
    private readonly IBroadcastRepository _broadcasts;

    public const int MaxHistoryPageSize = 100;
    public static readonly string SubjectTooLongMessage = $"Keep each subject to {BroadcastMessage.MaxSubjectLength} characters or fewer.";

    public BroadcastService(
        IUserRepository userRepository,
        ISessionRegistrationRepository registrationRepository,
        ISessionAttendanceRepository attendanceRepository,
        IEmailService emailService,
        INotificationService notificationService,
        IAuditLogRepository auditLogRepository,
        UnsubscribeLinks unsubscribeLinks,
        BroadcastQueue queue,
        ILogger<BroadcastService> logger,
        IBroadcastRepository broadcasts)
    {
        _broadcasts = broadcasts;
        _userRepository = userRepository;
        _registrationRepository = registrationRepository;
        _attendanceRepository = attendanceRepository;
        _emailService = emailService;
        _notificationService = notificationService;
        _auditLogRepository = auditLogRepository;
        _unsubscribeLinks = unsubscribeLinks;
        _queue = queue;
        _logger = logger;
    }

    public async Task<BroadcastAudiencePreviewDto> PreviewAudienceAsync(BroadcastAudience audience)
    {
        var emailRecipients = (await ResolveAudienceAsync(audience)).Where(ReceivesEmail).ToList();
        return new BroadcastAudiencePreviewDto
        {
            RecipientCount = emailRecipients.Count,
            SampleEmails = emailRecipients
                .Take(PreviewSampleSize)
                .Select(u => u.Email!)
                .ToList(),
        };
    }

    public async Task<SendBroadcastResultDto> QueueAsync(SendBroadcastRequestDto request, Guid? adminId, string adminName)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new ValidationException("Subject is required");
        if (string.IsNullOrWhiteSpace(request.BodyEn))
            throw new ValidationException("English body is required");
        if (request.Subject.Trim().Length > BroadcastMessage.MaxSubjectLength || (request.SubjectFr?.Trim().Length ?? 0) > BroadcastMessage.MaxSubjectLength)
            throw new ValidationException(SubjectTooLongMessage);

        var emailRecipients = (await ResolveAudienceAsync(request.Audience)).Count(ReceivesEmail);
        var record = new BroadcastMessage
        {
            Audience = (int)request.Audience,
            Subject = request.Subject.Trim(),
            SubjectFr = string.IsNullOrWhiteSpace(request.SubjectFr) ? null : request.SubjectFr.Trim(),
            BodyEn = request.BodyEn,
            BodyFr = string.IsNullOrWhiteSpace(request.BodyFr) ? null : request.BodyFr,
            SentByUserId = adminId,
            SentByName = string.IsNullOrWhiteSpace(adminName) ? "Admin" : adminName,
            Attempted = emailRecipients,
        };
        await _broadcasts.AddAsync(record);
        await _queue.EnqueueAsync(new QueuedBroadcast(request, adminId, adminName, record.Id));
        _logger.LogInformation("Broadcast queued — audience {Audience}, {Recipients} email recipients", request.Audience, emailRecipients);
        return new SendBroadcastResultDto { Attempted = emailRecipients, Queued = true, BroadcastId = record.Id };
    }

    public async Task<SendBroadcastResultDto> DeliverAsync(QueuedBroadcast broadcast)
    {
        var request = broadcast.Request;
        var audience = await ResolveAudienceAsync(request.Audience);
        var result = new SendBroadcastResultDto { Attempted = audience.Count(ReceivesEmail) };

        foreach (var user in audience)
        {
            var french = user.PreferredLanguage == EmailLanguage.French;
            var subject = french && !string.IsNullOrWhiteSpace(request.SubjectFr) ? request.SubjectFr! : request.Subject;
            var body = french && !string.IsNullOrWhiteSpace(request.BodyFr) ? request.BodyFr! : request.BodyEn;

            if (ReceivesEmail(user))
            {
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
            }

            // The in-app copy follows the user's in-app preference (checked by NotificationService), not email opt-in.
            await _notificationService.CreateAsync(
                user.Id,
                Domain.Entities.NotificationType.AdminBroadcast,
                title: subject,
                body: body,
                url: null);
        }

        _logger.LogInformation(
            "Broadcast delivered — audience {Audience}, attempted {Attempted}, succeeded {Succeeded}, failed {Failed}",
            request.Audience, result.Attempted, result.Succeeded, result.Failed);

        await WriteAuditAsync(request, result, broadcast.AdminId, broadcast.AdminName);
        if (broadcast.BroadcastId is Guid broadcastId)
        {
            var status = result.Attempted > 0 && result.Succeeded == 0 ? BroadcastStatus.Failed : BroadcastStatus.Sent;
            await _broadcasts.RecordDeliveryAsync(broadcastId, status, result.Attempted, result.Succeeded, result.Failed);
        }
        return result;
    }

    public Task MarkFailedAsync(Guid broadcastId) => _broadcasts.MarkFailedAsync(broadcastId);

    public async Task<BroadcastHistoryPageDto> GetHistoryAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxHistoryPageSize);
        var (items, total) = await _broadcasts.GetPageAsync(page, pageSize);
        return new BroadcastHistoryPageDto
        {
            Items = items.Select(b => Fill(new BroadcastHistoryItemDto(), b)).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<BroadcastDetailDto> GetBroadcastAsync(Guid id)
    {
        var message = await _broadcasts.GetByIdAsync(id) ?? throw new NotFoundException(nameof(BroadcastMessage), id);
        var detail = Fill(new BroadcastDetailDto(), message);
        detail.SubjectFr = message.SubjectFr;
        detail.BodyEn = message.BodyEn;
        detail.BodyFr = message.BodyFr;
        return detail;
    }

    private static T Fill<T>(T dto, BroadcastMessage message) where T : BroadcastHistoryItemDto
    {
        dto.Id = message.Id;
        dto.Audience = (BroadcastAudience)message.Audience;
        dto.Subject = message.Subject;
        dto.SentByName = message.SentByName;
        // SQL Server returns these without a kind; they are stored in UTC and must serialize with the Z.
        dto.QueuedAt = DateTime.SpecifyKind(message.QueuedAt, DateTimeKind.Utc);
        dto.CompletedAt = message.CompletedAt is DateTime completed ? DateTime.SpecifyKind(completed, DateTimeKind.Utc) : null;
        dto.Status = message.Status.ToString();
        dto.Attempted = message.Attempted;
        dto.Succeeded = message.Succeeded;
        dto.Failed = message.Failed;
        return dto;
    }

    private static bool ReceivesEmail(ApplicationUser user) =>
        !string.IsNullOrEmpty(user.Email) && user.EmailNotificationsEnabled;

    /// Turns the admin's plain-text body into escaped paragraphs inside the club email layout,
    /// keeping line breaks, and appends the unsubscribe footer CASL requires.
    public static string BuildEmailHtml(string subject, string body, string unsubscribeUrl, EmailLanguage language)
    {
        var paragraphs = body.Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(paragraph => EmailTemplateHelper.P(WebUtility.HtmlEncode(paragraph).Replace("\n", "<br/>")));

        var footer = UnsubscribeFooterHtml(unsubscribeUrl, language);

        return EmailTemplateHelper.BuildEmailLayout(subject, subject, string.Concat(paragraphs) + footer, language);
    }

    /// The unsubscribe line CASL requires on club updates (broadcasts and admin announcements).
    public static string UnsubscribeFooterHtml(string unsubscribeUrl, EmailLanguage language) =>
        "<p style='margin:24px 0 0;font-size:12px;line-height:1.5;color:#637369;'>" +
        EmailTemplateHelper.L(
            "You're receiving this because community updates are turned on in your SHB account.",
            "Vous recevez ce message parce que les nouvelles du club sont activées dans votre compte SHB.",
            language) +
        $" <a href='{WebUtility.HtmlEncode(unsubscribeUrl)}' style='color:{EmailTemplateHelper.ColorAccent};'>" +
        EmailTemplateHelper.L("Unsubscribe", "Se désabonner", language) +
        "</a></p>";

    /// Adds the unsubscribe line to an existing email, inside the body when the email has one.
    public static string AppendUnsubscribeFooter(string html, string unsubscribeUrl, EmailLanguage language)
    {
        var footer = UnsubscribeFooterHtml(unsubscribeUrl, language);
        var bodyEnd = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyEnd < 0 ? html + footer : html.Insert(bodyEnd, footer);
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
        // Broadcasts only reach players who kept community updates on. Email opt-out is applied
        // per channel in DeliverAsync; transactional emails (receipts, password reset, payment
        // confirmation) bypass both — standard CASL/CAN-SPAM convention.
        var all = (await _userRepository.GetActiveConfirmedUsersAsync())
            .Where(u => u.CommunityUpdatesEnabled)
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
        // Two queries for the whole club instead of two per player.
        var cutoff = DateTime.UtcNow.AddDays(-RecentNoShowLookbackDays);
        var candidateIds = candidates.Select(u => u.Id).ToHashSet();
        var registrations = (await _registrationRepository.GetRegistrationPairsInRangeAsync(cutoff, DateTime.UtcNow))
            .Where(r => candidateIds.Contains(r.UserId))
            .ToList();
        var attended = (await _attendanceRepository.GetAttendedPairsSinceAsync(cutoff)).ToHashSet();

        var noShowIds = registrations
            .GroupBy(r => r.UserId)
            .Where(g => (double)g.Count(attended.Contains) / g.Count() < NoShowThreshold)
            .Select(g => g.Key)
            .ToHashSet();
        return candidates.Where(u => noShowIds.Contains(u.Id)).ToList();
    }
}
