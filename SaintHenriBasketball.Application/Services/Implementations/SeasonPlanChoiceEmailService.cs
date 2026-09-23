using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Email;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.Templates;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

/// <summary>
/// A week before a season starts, asks every player how they want to pay for it.
///
/// The send is recorded per player and per season, so the daily job can run as often as it likes,
/// a restart cannot repeat it, and an admin sending by hand cannot double up on the job.
/// </summary>
public class SeasonPlanChoiceEmailService : ISeasonPlanChoiceEmailService
{
    /// The reminder-log kind that records this email, so it is never sent to the same player twice.
    public const string ReminderKind = "SeasonPlanChoice";
    public const int DefaultDaysAhead = 7;

    public const string NoSeasonOutcome = "No season starts on that day.";
    public const string FlagOffOutcome = "The season-plan-choice-email flag is off.";
    public const string AutoSendOffOutcome = "Automatic sending is off; an admin sends this email by hand.";

    private readonly ISeasonRepository _seasons;
    private readonly IUserRepository _users;
    private readonly ISessionRepository _sessions;
    private readonly ISeasonPlanChoiceRepository _choices;
    private readonly IOutstandingBalancesRepository _reminders;
    private readonly IEmailService _email;
    private readonly IFeatureFlagService _flags;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SeasonPlanChoiceEmailService> _logger;

    public SeasonPlanChoiceEmailService(
        ISeasonRepository seasons,
        IUserRepository users,
        ISessionRepository sessions,
        ISeasonPlanChoiceRepository choices,
        IOutstandingBalancesRepository reminders,
        IEmailService email,
        IFeatureFlagService flags,
        IConfiguration configuration,
        ILogger<SeasonPlanChoiceEmailService> logger)
    {
        _seasons = seasons;
        _users = users;
        _sessions = sessions;
        _choices = choices;
        _reminders = reminders;
        _email = email;
        _flags = flags;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PlanChoiceSendResultDto> RunScheduledAsync()
    {
        // The daily job's only entry point. Sending unattended needs its own switch, so that turning
        // the feature on to send by hand cannot also set the job loose on the next season.
        if (!await _flags.IsEnabledAsync(FeatureFlagKeys.SeasonPlanChoiceEmailAuto))
            return new PlanChoiceSendResultDto { Outcome = AutoSendOffOutcome };

        if (!await _flags.IsEnabledAsync(FeatureFlagKeys.SeasonPlanChoiceEmail))
            return new PlanChoiceSendResultDto { Outcome = FlagOffOutcome };

        // A window, not a single day.
        //
        // This used to ask for a season starting exactly a week out, which gives the club one
        // firing — 10 AM, once — to be switched on, deployed, and working. Miss it and the job
        // reports "No season starts on that day" every morning afterwards while the season comes
        // and goes: a silent failure that looks identical to having nothing to do. That is what
        // happened for the 26 September season.
        //
        // Running every day over a window is only safe because sending is idempotent: SendAsync
        // records a ReminderLog per player per season, so the first run that finds the season
        // sends, and every run after it sends nothing.
        var today = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;

        var seasons = await _seasons.GetAllAsync();
        var season = seasons
            .Where(s => s.Status != SeasonStatus.Closed
                        && s.StartDate.Date > today
                        && s.StartDate.Date <= today.AddDays(DefaultDaysAhead))
            .OrderBy(s => s.StartDate)
            .FirstOrDefault();

        if (season == null)
            return new PlanChoiceSendResultDto { DaysUntilStart = DefaultDaysAhead, Outcome = NoSeasonOutcome };

        // The real number of days left, which is what the email tells the player — not the width
        // of the window it was found in.
        return await SendAsync(season, (season.StartDate.Date - today).Days, dryRun: false);
    }

    public async Task<PlanChoiceSendResultDto> RunForSeasonStartingInAsync(int daysAhead, bool dryRun = false)
    {
        if (!await _flags.IsEnabledAsync(FeatureFlagKeys.SeasonPlanChoiceEmail))
            return new PlanChoiceSendResultDto { DryRun = dryRun, Outcome = FlagOffOutcome };

        var today = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;
        var target = today.AddDays(daysAhead);

        var seasons = await _seasons.GetAllAsync();
        var season = seasons.FirstOrDefault(s => s.StartDate.Date == target && s.Status != SeasonStatus.Closed);
        if (season == null)
            return new PlanChoiceSendResultDto { DryRun = dryRun, DaysUntilStart = daysAhead, Outcome = NoSeasonOutcome };

        return await SendAsync(season, daysAhead, dryRun);
    }

    public async Task<PlanChoiceSendResultDto> RunForSeasonAsync(Guid seasonId, bool dryRun = false)
    {
        var season = await _seasons.GetByIdAsync(seasonId) ?? throw new NotFoundException("Season not found.");
        var today = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;
        return await SendAsync(season, (season.StartDate.Date - today).Days, dryRun);
    }

    public async Task<string> PreviewAsync(Guid seasonId, EmailLanguage language)
    {
        var season = await _seasons.GetByIdAsync(seasonId) ?? throw new NotFoundException("Season not found.");
        var today = SessionTimeHelper.ToLocal(DateTime.UtcNow).Date;
        var model = await BuildModelAsync(season, (season.StartDate.Date - today).Days);
        model.FirstName = "Jeanne";
        return EmailTemplates.Season.GetSeasonPlanChoiceEmail(model, language);
    }

    private async Task<PlanChoiceSendResultDto> SendAsync(Season season, int daysAhead, bool dryRun)
    {
        var result = new PlanChoiceSendResultDto
        {
            SeasonId = season.Id,
            SeasonName = season.Name,
            StartDate = season.StartDate,
            DaysUntilStart = daysAhead,
            DryRun = dryRun,
        };

        var model = await BuildModelAsync(season, daysAhead);

        var users = (await _users.GetAllUsersAsync()).ToList();

        // Anyone who has already paid for a pass has nothing to choose.
        var paidUserIds = new HashSet<Guid>();
        foreach (var user in users)
        {
            if (await _choices.HasPaidPassAsync(season.Id, user.Id)) paidUserIds.Add(user.Id);
        }

        // Who already has this email for this season. The ids matter: asked for nobody, the
        // repository answers with nobody, and every run would look like the first one.
        var alreadyEmailed = (await _reminders.GetLastSentAsync(users.Select(u => u.Id).ToList()))
            .Where(r => r.Kind == ReminderKind && r.SeasonId == season.Id)
            .Select(r => r.UserId)
            .ToHashSet();

        var logs = new List<ReminderLog>();

        foreach (var user in users)
        {
            if (user.IsAdmin || user.IsDeactivated || string.IsNullOrWhiteSpace(user.Email) || !user.EmailConfirmed)
            {
                result.Skipped++;
                continue;
            }
            if (paidUserIds.Contains(user.Id))
            {
                result.Skipped++;
                continue;
            }
            if (alreadyEmailed.Contains(user.Id))
            {
                result.AlreadySent++;
                continue;
            }

            if (dryRun)
            {
                result.Sent++;
                if (result.Recipients.Count < 200) result.Recipients.Add(user.Email!);
                continue;
            }

            try
            {
                model.FirstName = user.FirstName;
                var html = EmailTemplates.Season.GetSeasonPlanChoiceEmail(model, user.PreferredLanguage);
                var subject = EmailTemplateHelper.LSubject(
                    $"Choose your plan — {season.Name} starts soon",
                    $"Choisissez votre formule — {season.Name} commence bientôt",
                    user.PreferredLanguage);

                await _email.SendEmailAsync(user.Email!, subject, html);
                result.Sent++;
                logs.Add(new ReminderLog
                {
                    UserId = user.Id,
                    SeasonId = season.Id,
                    Kind = ReminderKind,
                    Status = ReminderStatuses.Sent,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Plan choice email failed for {UserId} and season {SeasonId}", user.Id, season.Id);
                result.Failed++;
                logs.Add(new ReminderLog
                {
                    UserId = user.Id,
                    SeasonId = season.Id,
                    Kind = ReminderKind,
                    Status = ReminderStatuses.Failed,
                    Reason = ex.Message.Length > ReminderLog.MaxReasonLength ? ex.Message[..ReminderLog.MaxReasonLength] : ex.Message,
                });
            }
        }

        // Only a successful send is recorded as sent, so a failure is retried tomorrow rather than lost.
        var sentLogs = logs.Where(l => l.Status == ReminderStatuses.Sent).ToList();
        if (sentLogs.Count > 0) await _reminders.AddLogsAsync(sentLogs);

        _logger.LogInformation("Plan choice email for season {SeasonId}: {Sent} sent, {Already} already had it, {Skipped} skipped, {Failed} failed",
            season.Id, result.Sent, result.AlreadySent, result.Skipped, result.Failed);

        return result;
    }

    private async Task<SeasonPlanChoiceEmailModel> BuildModelAsync(Season season, int daysAhead)
    {
        // Spot holders, not paid passes. Counting only what has been paid for made this email
        // advertise seats that the plan page would then refuse, because a player who has chosen a
        // pass holds it before the money arrives — and an email whose whole purpose is to get
        // people to claim a spot is the worst place to overstate how many are left.
        var taken = (await _choices.GetSpotHolderIdsAsync(season.Id, includeProfilePlan: true)).Count;
        var sessions = (await _sessions.GetUpcomingSessionsAsync())
            .Where(s => s.Status != SessionStatus.Cancelled
                        && s.SessionDate.Date >= season.StartDate.Date
                        && s.SessionDate.Date <= season.EndDate.Date)
            .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
            .ToList();
        var first = sessions.FirstOrDefault();

        return new SeasonPlanChoiceEmailModel
        {
            SeasonName = season.Name,
            StartDate = season.StartDate,
            EndDate = season.EndDate,
            DaysUntilStart = daysAhead,
            PassPrice = season.Price,
            // Drop-in is priced per session, so the first session of the season is the one that applies.
            DropInPrice = first?.DropInPrice,
            PassCapacity = season.SeasonPassCapacity,
            PassesLeft = Math.Max(0, season.SeasonPassCapacity - taken),
            FirstSessionDate = first?.SessionDate,
            FirstSessionStart = first?.StartTime,
            FirstSessionEnd = first?.EndTime,
            Location = first?.Location,
            SessionCount = sessions.Count,
            AppUrl = _configuration["AppUrl"] ?? "https://sainthenribasketball.com",
        };
    }
}
