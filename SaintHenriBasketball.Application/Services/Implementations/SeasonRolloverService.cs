using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.SeasonRollover;
using SaintHenriBasketball.Application.DTOs.Session;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class SeasonRolloverService : ISeasonRolloverService
{
    public const string AuditEntityType = "Season";
    public const string InvitesSentAction = "Season.RenewalInvitesSent";
    public const string DraftCreatedAction = "Season.RolloverDraftCreated";

    public const string RuleCompletedPayments = "completedSeasonPayments";
    public const string RuleSeasonPlanPlayers = "seasonPlanPlayers";

    public const string SkipDeactivated = "deactivated";
    public const string SkipNoEmail = "noEmail";
    public const string SkipCommunityUpdatesOff = "communityUpdatesOff";
    public const string SkipEmailNotificationsOff = "emailNotificationsOff";
    public const string SkipAlreadyRenewed = "alreadyRenewed";

    private const int MaxNameLength = 100;
    private const string AllSeasonsCacheKey = "AllSeasons";
    private static readonly Regex YearPattern = new(@"(?<!\d)(?:19|20)\d{2}(?!\d)", RegexOptions.Compiled);
    private static readonly CultureInfo English = new("en-CA");
    private static readonly CultureInfo French = new("fr-CA");

    private readonly ISeasonRepository _seasons;
    private readonly ISeasonRolloverRepository _rollover;
    private readonly ISessionService _sessionService;
    private readonly IAuditLogService _auditLog;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IEmailService _emailService;
    private readonly UnsubscribeLinks _unsubscribeLinks;
    private readonly IConfiguration _configuration;
    private readonly ICacheService _cache;
    private readonly ILogger<SeasonRolloverService> _logger;

    public SeasonRolloverService(
        ISeasonRepository seasons,
        ISeasonRolloverRepository rollover,
        ISessionService sessionService,
        IAuditLogService auditLog,
        IAuditLogRepository auditLogs,
        IEmailService emailService,
        UnsubscribeLinks unsubscribeLinks,
        IConfiguration configuration,
        ICacheService cache,
        ILogger<SeasonRolloverService> logger)
    {
        _seasons = seasons;
        _rollover = rollover;
        _sessionService = sessionService;
        _auditLog = auditLog;
        _auditLogs = auditLogs;
        _emailService = emailService;
        _unsubscribeLinks = unsubscribeLinks;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SeasonRolloverPreviewDto> PreviewAsync(Guid sourceSeasonId, SeasonRolloverRequestDto? request)
    {
        var source = await GetSeasonAsync(sourceSeasonId);
        var proposal = await ProposeAsync(source, request ?? new SeasonRolloverRequestDto());

        var existing = (await _rollover.GetSessionsBetweenAsync(proposal.Start, proposal.End))
            .Where(s => proposal.Saturdays.Contains(s.SessionDate.Date))
            .ToList();
        var takenDates = existing.Select(s => s.SessionDate.Date).ToHashSet();
        var overlapping = await _rollover.GetOverlappingSeasonsAsync(proposal.Start, proposal.End);
        var duplicate = await _rollover.SeasonExistsWithDatesAsync(proposal.Start, proposal.End);
        var plan = proposal.SessionPlan;

        var sessions = proposal.Saturdays.Select(date => new RolloverSessionDto
        {
            Date = CalendarDate(date),
            StartTime = plan.StartTime,
            EndTime = plan.EndTime,
            StartsAtUtc = DateTime.SpecifyKind(SessionTimeHelper.ToUtc(SessionTimeHelper.CombineLocal(date, plan.StartTime)), DateTimeKind.Utc),
            MaxCapacity = plan.MaxCapacity,
            DropInPrice = plan.DropInPrice,
            Location = plan.Location,
            AlreadyExists = takenDates.Contains(date),
        }).ToList();

        return new SeasonRolloverPreviewDto
        {
            SourceSeason = Summary(source),
            ProposedSeason = new RolloverProposedSeasonDto
            {
                Name = proposal.Name,
                StartDate = Utc(proposal.Start),
                EndDate = Utc(proposal.End),
                Price = proposal.Price,
                Notes = source.Notes,
                LengthDays = (proposal.End - proposal.Start).Days,
                OverriddenFields = proposal.OverriddenFields,
            },
            SessionPlan = plan,
            Sessions = sessions,
            SessionsToCreate = sessions.Count(s => !s.AlreadyExists),
            SessionsToSkip = sessions.Count(s => s.AlreadyExists),
            Conflicts = new RolloverConflictsDto
            {
                DuplicateSeason = duplicate,
                OverlappingSeasons = overlapping.Select(Summary).ToList(),
                ExistingSessions = existing.Select(s => new RolloverExistingSessionDto
                {
                    Id = s.Id,
                    Date = CalendarDate(s.SessionDate),
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Location = s.Location,
                    Status = s.Status,
                }).ToList(),
            },
            CanCreate = !duplicate,
        };
    }

    public async Task<SeasonRolloverDraftResultDto> CreateDraftAsync(Guid sourceSeasonId, SeasonRolloverRequestDto? request, Guid? adminId, string adminName)
    {
        var source = await GetSeasonAsync(sourceSeasonId);
        var proposal = await ProposeAsync(source, request ?? new SeasonRolloverRequestDto());

        if (await _rollover.SeasonExistsWithDatesAsync(proposal.Start, proposal.End))
            throw new ValidationException($"A season from {proposal.Start:yyyy-MM-dd} to {proposal.End:yyyy-MM-dd} already exists.");

        // Closed is the existing "not current" state: the current season is the Open one, and an Open
        // season blocks opening another. The admin opens the draft when the source season is closed.
        var season = new Season(Utc(proposal.Start), Utc(proposal.End), proposal.Price, source.Notes)
        {
            Name = proposal.Name,
            Status = SeasonStatus.Closed,
        };
        await _seasons.AddAsync(season);
        await _cache.RemoveAsync(AllSeasonsCacheKey);

        var plan = proposal.SessionPlan;
        var generated = await _sessionService.GenerateSessionsForSeasonAsync(new GenerateSessionsDto
        {
            StartDate = proposal.Start,
            EndDate = proposal.End,
            StartTime = plan.StartTime,
            EndTime = plan.EndTime,
            MaxCapacity = plan.MaxCapacity,
            DropInPrice = plan.DropInPrice,
            Location = plan.Location,
        });

        await TryAuditAsync(DraftCreatedAction, season.Id,
            $"Rolled over from {source.Name} ({source.Id}); {generated.Created} sessions created, {generated.Skipped} skipped",
            adminId, adminName);

        return new SeasonRolloverDraftResultDto
        {
            SeasonId = season.Id,
            SourceSeasonId = source.Id,
            Name = season.Name,
            StartDate = Utc(proposal.Start),
            EndDate = Utc(proposal.End),
            Price = season.Price,
            Status = season.Status,
            SessionsCreated = generated.Created,
            SessionsSkipped = generated.Skipped,
            CreatedDates = generated.CreatedDates.Select(CalendarDate).ToList(),
            SkippedDates = generated.SkippedDates.Select(CalendarDate).ToList(),
        };
    }

    public const string NewSeasonClosedMessage = "Open the new season before inviting players to renew (close the current season first).";

    // One invite run per new season at a time within this process, so a double click or a retry waits for the
    // first run and then finds its audit entry instead of sending again. It does not coordinate several API
    // instances; across instances the audit lookup is the only protection. One small semaphore per season is kept.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim> InviteGates = new();

    public async Task<SeasonRenewalInviteResultDto> SendRenewalInvitesAsync(Guid newSeasonId, SeasonRenewalInviteRequestDto request, Guid? adminId, string adminName)
    {
        if (request is null || request.SourceSeasonId == Guid.Empty)
            throw new ValidationException("Choose the season whose players should be invited.");
        if (request.SourceSeasonId == newSeasonId)
            throw new ValidationException("The source season must be different from the new season.");

        var newSeason = await GetSeasonAsync(newSeasonId);
        var source = await GetSeasonAsync(request.SourceSeasonId);
        // Players can't register or pay for a closed season, so an invitation would lead nowhere.
        if (newSeason.Status == SeasonStatus.Closed)
            throw new ValidationException(NewSeasonClosedMessage);

        var gate = InviteGates.GetOrAdd(newSeason.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try { return await SendInvitesAsync(newSeason, source, request, adminId, adminName); }
        finally { gate.Release(); }
    }

    private async Task<SeasonRenewalInviteResultDto> SendInvitesAsync(Season newSeason, Season source, SeasonRenewalInviteRequestDto request, Guid? adminId, string adminName)
    {
        var result = new SeasonRenewalInviteResultDto { SeasonId = newSeason.Id, SourceSeasonId = source.Id };

        var previous = (await _auditLogs.GetByEntityAsync(AuditEntityType, newSeason.Id))
            .Where(a => a.Action == InvitesSentAction)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();
        if (previous != null)
        {
            result.PreviouslySentAt = DateTime.SpecifyKind(previous.CreatedAt, DateTimeKind.Utc);
            if (!request.Resend)
            {
                result.AlreadySent = true;
                return result;
            }
        }

        // Players who paid for the source season. Older season payments were never linked to a season
        // (SeasonId is null), so when none are linked the Season-plan players stand in for them.
        var players = await _rollover.GetPlayersWithCompletedSeasonPaymentAsync(source.Id);
        result.RecipientRule = RuleCompletedPayments;
        if (players.Count == 0)
        {
            players = await _rollover.GetSeasonPlanPlayersAsync();
            result.RecipientRule = RuleSeasonPlanPlayers;
        }
        result.Candidates = players.Count;

        var renewed = (await _rollover.GetUserIdsWithSeasonPaymentAsync(newSeason.Id)).ToHashSet();
        var firstSession = (await _rollover.GetSessionsBetweenAsync(newSeason.StartDate, newSeason.EndDate))
            .FirstOrDefault(s => s.Status != SessionStatus.Cancelled);
        var appUrl = (_configuration["AppUrl"] ?? "https://sainthenribasketball.com").TrimEnd('/');

        foreach (var player in players.OrderBy(p => p.LastName).ThenBy(p => p.FirstName))
        {
            var reason = SkipReason(player, renewed);
            if (reason != null)
            {
                result.SkippedPlayers.Add(new RenewalInvitePlayerDto { UserId = player.Id, Name = FullName(player), Reason = reason });
                continue;
            }

            try
            {
                var language = player.PreferredLanguage;
                var subject = EmailTemplateHelper.LSubject($"Renew for {newSeason.Name}", $"Renouvelez pour {newSeason.Name}", language);
                var html = BuildInviteHtml(player, source, newSeason, firstSession, appUrl);
                html = BroadcastService.AppendUnsubscribeFooter(html, _unsubscribeLinks.CreateUrl(player.Id), language);
                await _emailService.SendEmailAsync(player.Email, subject, html);
                result.Sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Renewal invite failed for user {UserId}, season {SeasonId}", player.Id, newSeason.Id);
                result.FailedPlayers.Add(new RenewalInvitePlayerDto { UserId = player.Id, Name = FullName(player) });
                result.Failed++;
            }
        }

        result.Skipped = result.SkippedPlayers.Count;
        result.SkippedReasons = result.SkippedPlayers
            .GroupBy(p => p.Reason!)
            .Select(g => new RenewalInviteSkipCountDto { Reason = g.Key, Count = g.Count() })
            .OrderByDescending(r => r.Count)
            .ToList();

        // Only a run that reached someone blocks the next one; if every send failed the admin can simply retry.
        if (result.Sent > 0)
        {
            await TryAuditAsync(InvitesSentAction, newSeason.Id,
                $"Source season {source.Name} ({source.Id}); rule: {result.RecipientRule}; sent: {result.Sent}; skipped: {result.Skipped}; failed: {result.Failed}; resend: {request.Resend}",
                adminId, adminName);
        }

        _logger.LogInformation("Renewal invites for season {SeasonId}: sent {Sent}, skipped {Skipped}, failed {Failed}",
            newSeason.Id, result.Sent, result.Skipped, result.Failed);
        return result;
    }

    private sealed record Proposal(string Name, DateTime Start, DateTime End, decimal Price, List<string> OverriddenFields,
        List<DateTime> Saturdays, RolloverSessionPlanDto SessionPlan);

    private async Task<Proposal> ProposeAsync(Season source, SeasonRolloverRequestDto request)
    {
        var overridden = new List<string>();
        var sourceStart = source.StartDate.Date;
        var lengthDays = Math.Max(1, (source.EndDate.Date - sourceStart).Days);

        // The club plays Saturdays, so the next season opens on the first Saturday after the last one ends
        // and keeps its length. Any start date a request supplies is taken as given.
        var start = NextSaturdayAfter(source.EndDate.Date);
        if (request.StartDate is DateTime requestedStart)
        {
            start = requestedStart.Date;
            overridden.Add("startDate");
        }
        var end = start.AddDays(lengthDays);
        if (request.EndDate is DateTime requestedEnd)
        {
            end = requestedEnd.Date;
            overridden.Add("endDate");
        }
        if (end <= start)
            throw new ValidationException("End date must be after start date");

        var price = source.Price;
        if (request.Price is decimal requestedPrice)
        {
            if (requestedPrice < 0) throw new ValidationException("Price must be greater than or equal to 0");
            if (decimal.Round(requestedPrice, 2) != requestedPrice) throw new ValidationException("Price cannot have more than 2 decimal places");
            price = requestedPrice;
            overridden.Add("price");
        }

        string name;
        if (request.Name != null)
        {
            name = request.Name.Trim();
            if (name.Length == 0) throw new ValidationException("Season name is required");
            overridden.Add("name");
        }
        else
        {
            name = NextName(source.Name, sourceStart, start);
        }
        if (name.Length > MaxNameLength)
            throw new ValidationException($"Season name cannot exceed {MaxNameLength} characters");

        var saturdays = new List<DateTime>();
        // Same walk as SessionService.GenerateSessionsForSeasonAsync, so the preview matches what gets created.
        for (var date = NextSaturdayOnOrAfter(start); date <= end; date = date.AddDays(7))
            saturdays.Add(date);

        return new Proposal(name, start, end, price, overridden, saturdays, await SessionPlanAsync(source));
    }

    private async Task<RolloverSessionPlanDto> SessionPlanAsync(Season source)
    {
        var latest = (await _rollover.GetSessionsBetweenAsync(source.StartDate, source.EndDate))
            .Where(s => s.Status != SessionStatus.Cancelled)
            .OrderByDescending(s => s.SessionDate.DayOfWeek == DayOfWeek.Saturday)
            .ThenByDescending(s => s.SessionDate)
            .FirstOrDefault();
        var defaults = new GenerateSessionsDto();
        if (latest == null)
        {
            return new RolloverSessionPlanDto
            {
                StartTime = defaults.StartTime,
                EndTime = defaults.EndTime,
                MaxCapacity = defaults.MaxCapacity,
                DropInPrice = defaults.DropInPrice,
                Location = defaults.Location,
            };
        }
        return new RolloverSessionPlanDto
        {
            StartTime = latest.StartTime,
            EndTime = latest.EndTime,
            MaxCapacity = latest.MaxCapacity,
            DropInPrice = latest.DropInPrice,
            Location = string.IsNullOrWhiteSpace(latest.Location) ? defaults.Location : latest.Location,
            BasedOnSessionId = latest.Id,
        };
    }

    /// Shifts every year in the name by the years between the two seasons, at least one ("Fall 2025" → "Fall 2026",
    /// "2025-2026" → "2026-2027"). A name without a year is kept.
    public static string NextName(string? sourceName, DateTime sourceStart, DateTime newStart)
    {
        if (string.IsNullOrWhiteSpace(sourceName)) return $"Season {newStart:yyyy}";
        var shift = Math.Max(1, newStart.Year - sourceStart.Year);
        return YearPattern.Replace(sourceName.Trim(), m => (int.Parse(m.Value, CultureInfo.InvariantCulture) + shift).ToString(CultureInfo.InvariantCulture));
    }

    private static string? SkipReason(ApplicationUser player, HashSet<Guid> renewed)
    {
        if (player.IsDeactivated) return SkipDeactivated;
        if (string.IsNullOrWhiteSpace(player.Email)) return SkipNoEmail;
        if (!player.CommunityUpdatesEnabled) return SkipCommunityUpdatesOff;
        if (!player.EmailNotificationsEnabled) return SkipEmailNotificationsOff;
        if (renewed.Contains(player.Id)) return SkipAlreadyRenewed;
        return null;
    }

    private static string BuildInviteHtml(ApplicationUser player, Season source, Season newSeason, Session? firstSession, string appUrl)
    {
        var language = player.PreferredLanguage;
        var newName = WebUtility.HtmlEncode(newSeason.Name);
        var sourceName = WebUtility.HtmlEncode(source.Name);

        var fields = new Dictionary<string, string?>
        {
            [EmailTemplateHelper.L("Season", "Saison", language)] = newName,
            [EmailTemplateHelper.L("Dates", "Dates", language)] = EmailTemplateHelper.L(
                $"{newSeason.StartDate.ToString("MMMM d, yyyy", English)} – {newSeason.EndDate.ToString("MMMM d, yyyy", English)}",
                $"{newSeason.StartDate.ToString("d MMMM yyyy", French)} au {newSeason.EndDate.ToString("d MMMM yyyy", French)}",
                language),
            [EmailTemplateHelper.L("Season fee", "Frais de saison", language)] = EmailTemplateHelper.L(
                newSeason.Price.ToString("C", English), newSeason.Price.ToString("C", French), language),
        };
        if (firstSession != null)
        {
            var times = $"{SessionTimeHelper.FormatDisplay(firstSession.StartTime)}–{SessionTimeHelper.FormatDisplay(firstSession.EndTime)}";
            fields[EmailTemplateHelper.L("Schedule", "Horaire", language)] = EmailTemplateHelper.L($"Saturdays, {times}", $"Les samedis, {times}", language);
        }

        var content =
            EmailTemplateHelper.Greeting(WebUtility.HtmlEncode(player.FirstName), language) +
            EmailTemplateHelper.P(EmailTemplateHelper.L(
                $"Thanks for playing with us during {sourceName}. The next season is coming up and we'd love to have you back.",
                $"Merci d'avoir joué avec nous pendant {sourceName}. La prochaine saison approche et on aimerait vous revoir.",
                language)) +
            EmailTemplateHelper.BuildInfoBox(fields) +
            EmailTemplateHelper.P(EmailTemplateHelper.L(
                "Renew your season pass in the app to keep your spot.",
                "Renouvelez votre passe de saison dans l'application pour garder votre place.",
                language)) +
            EmailTemplateHelper.BuildButton("Renew my season", "Renouveler ma saison", appUrl, language);

        return EmailTemplateHelper.BuildEmailLayout($"Renew for {newName}", $"Renouvelez pour {newName}", content, language);
    }

    private async Task TryAuditAsync(string action, Guid seasonId, string details, Guid? adminId, string adminName)
    {
        try
        {
            await _auditLog.LogAsync(action, AuditEntityType, seasonId, details.Length > 500 ? details[..500] : details,
                adminId, string.IsNullOrWhiteSpace(adminName) ? "Admin" : adminName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit entry {Action} failed for season {SeasonId}", action, seasonId);
        }
    }

    private async Task<Season> GetSeasonAsync(Guid id) =>
        await _seasons.GetByIdAsync(id) ?? throw new NotFoundException($"Season with ID {id} not found");

    private static DateTime NextSaturdayAfter(DateTime date) => NextSaturdayOnOrAfter(date.Date.AddDays(1));

    private static DateTime NextSaturdayOnOrAfter(DateTime date)
    {
        var current = date.Date;
        while (current.DayOfWeek != DayOfWeek.Saturday) current = current.AddDays(1);
        return current;
    }

    private static DateTime Utc(DateTime date) => DateTime.SpecifyKind(date, DateTimeKind.Utc);

    // Session dates are Montreal calendar dates stored at midnight without a kind; keep them that way.
    private static DateTime CalendarDate(DateTime date) => DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);

    private static string FullName(ApplicationUser user) => $"{user.FirstName} {user.LastName}".Trim();

    private static RolloverSeasonSummaryDto Summary(Season season) => new()
    {
        Id = season.Id,
        Name = season.Name,
        StartDate = Utc(season.StartDate),
        EndDate = Utc(season.EndDate),
        Price = season.Price,
        Notes = season.Notes,
        Status = season.Status,
    };
}
