using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

/// Per-season plan choice: what a player picked for a given season, how many season passes are
/// still available, and the admin reset.
///
/// A spot is held by a COMPLETED PAYMENT, never by a choice. That is the rule the whole feature
/// turns on: an unpaid choice reserves nothing and is exactly what a reset clears.
public class SeasonPlanService : ISeasonPlanService
{
    public const string SoldOutMessage = "The season pass is sold out for this season.";

    private readonly ISeasonRepository _seasons;
    private readonly ISeasonPlanChoiceRepository _choices;
    private readonly IUserRepository _users;
    private readonly IAuditLogService _audit;
    private readonly IEmailService _email;
    private readonly IFeatureFlagService _flags;
    private readonly ISessionRepository _sessions;
    private readonly ILogger<SeasonPlanService> _logger;

    public SeasonPlanService(
        ISeasonRepository seasons,
        ISeasonPlanChoiceRepository choices,
        IUserRepository users,
        IAuditLogService audit,
        IEmailService email,
        IFeatureFlagService flags,
        ISessionRepository sessions,
        ILogger<SeasonPlanService> logger)
    {
        _seasons = seasons;
        _choices = choices;
        _users = users;
        _audit = audit;
        _email = email;
        _flags = flags;
        _sessions = sessions;
        _logger = logger;
    }

    public async Task<SeasonPlanStateDto?> GetCurrentAsync(Guid userId)
    {
        // GetCurrentSeasonAsync filters on Status == Open alone — it ignores the dates (it even
        // declares an unused `now`). That is the behaviour we want here and it is deliberate: the
        // open season is the one a player is being asked to pay for, including in the days before
        // it starts. Do not "fix" this call to require today to fall inside the dates.
        var season = await _seasons.GetCurrentSeasonAsync();
        if (season is null) return null;

        return await BuildStateAsync(season.Id, season.Name, season.StartDate, season.EndDate,
            season.Price, season.SeasonPassCapacity, userId);
    }

    public async Task<SeasonPlanStateDto> ChooseAsync(Guid userId, PaymentPlan plan)
    {
        var season = await _seasons.GetCurrentSeasonAsync()
            ?? throw new ValidationException("No season is open right now.");

        // Read before writing: an email only makes sense when the answer actually changed, and a
        // player re-saving the same plan should not be told again.
        var previous = (await _choices.GetAsync(season.Id, userId))?.Plan;
        var user = await _users.GetByIdAsync(userId);

        if (plan == PaymentPlan.Season)
        {
            // Counting the spots and claiming one happen together under a lock on the season, so two
            // players cannot both read the last spot as free. A player who already holds one is
            // always let through: re-affirming a pass they hold must never be refused.
            if (!await _choices.TryTakeSeasonSpotAsync(season.Id, userId, season.SeasonPassCapacity))
                throw new ValidationException(SoldOutMessage);
        }
        else
        {
            await _choices.UpsertAsync(season.Id, userId, plan);

            // Keep the denormalised current plan coherent: everything else in the app reads it. The
            // season branch sets it inside the lock, as part of taking the spot.
            if (user is not null && user.PaymentPlan != plan)
            {
                user.PaymentPlan = plan;
                await _users.UpdateAsync(user);
            }
        }

        _logger.LogInformation("User {UserId} chose {Plan} for season {SeasonId}", userId, plan, season.Id);

        if (previous != plan && user is not null)
            await ConfirmByEmailAsync(user, season, plan);

        return await BuildStateAsync(season.Id, season.Name, season.StartDate, season.EndDate,
            season.Price, season.SeasonPassCapacity, userId);
    }

    public async Task<SeasonPlanResetDto> PreviewResetAsync(Guid seasonId)
    {
        var season = await _seasons.GetByIdAsync(seasonId)
            ?? throw new NotFoundException($"Season with ID {seasonId} not found");

        var unpaid = await _choices.GetUnpaidChoiceUserIdsAsync(seasonId);
        var paid = await _choices.CountPaidPassesAsync(seasonId);

        return new SeasonPlanResetDto
        {
            SeasonId = season.Id,
            SeasonName = season.Name,
            Cleared = unpaid.Count,
            KeptPaid = paid,
        };
    }

    public async Task<SeasonPlanResetDto> ResetAsync(Guid seasonId, Guid adminId, string adminName)
    {
        var season = await _seasons.GetByIdAsync(seasonId)
            ?? throw new NotFoundException($"Season with ID {seasonId} not found");

        // Read who is affected BEFORE clearing, so the same players get put back on drop-in.
        var affected = await _choices.GetUnpaidChoiceUserIdsAsync(seasonId);
        var paid = await _choices.CountPaidPassesAsync(seasonId);
        var cleared = await _choices.ClearUnpaidForSeasonAsync(seasonId);

        if (affected.Count > 0)
        {
            // One query for every affected player rather than one round-trip each: a reset over a
            // whole club is exactly where per-row loads turn a click into a timeout.
            var users = await _users.GetUsersByIdsAsync(affected);
            foreach (var user in users.Where(u => u.PaymentPlan != PaymentPlan.DropIn))
            {
                user.PaymentPlan = PaymentPlan.DropIn;
                await _users.UpdateAsync(user);
            }
        }

        await _audit.LogAsync(
            "Season.PlanChoicesReset",
            "Season",
            seasonId,
            $"Cleared {cleared} unpaid plan choice(s) for {season.Name}; {paid} paid pass(es) kept",
            adminId,
            adminName);

        _logger.LogInformation(
            "Admin {AdminId} reset season {SeasonId}: {Cleared} cleared, {Paid} paid kept",
            adminId, seasonId, cleared, paid);

        return new SeasonPlanResetDto
        {
            SeasonId = season.Id,
            SeasonName = season.Name,
            Cleared = cleared,
            KeptPaid = paid,
        };
    }

    /// Tells the player what they just chose. Never lets a mail problem undo the choice: the plan is
    /// already saved by this point, and failing here would report an error for work that succeeded.
    private async Task ConfirmByEmailAsync(Domain.Entities.ApplicationUser user, Domain.Entities.Season season, PaymentPlan plan)
    {
        try
        {
            if (!await _flags.IsEnabledAsync(FeatureFlagKeys.PlanChoiceConfirmationEmail)) return;

            var holders = await _choices.GetSpotHolderIdsAsync(season.Id, includeProfilePlan: true);
            var firstSession = (await _sessions.GetUpcomingSessionsAsync())
                .Where(s => s.Status != SessionStatus.Cancelled
                            && s.SessionDate.Date >= season.StartDate.Date
                            && s.SessionDate.Date <= season.EndDate.Date)
                .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
                .FirstOrDefault();

            await _email.SendPlanChoiceConfirmationAsync(user, new DTOs.Email.PlanChoiceConfirmationEmailModel
            {
                SeasonName = season.Name,
                Plan = plan,
                StartDate = season.StartDate,
                EndDate = season.EndDate,
                PassPrice = season.Price,
                DropInPrice = firstSession?.DropInPrice,
                AlreadyPaid = await _choices.HasPaidPassAsync(season.Id, user.Id),
                SpotsLeft = Math.Max(0, season.SeasonPassCapacity - holders.Count),
                AppUrl = "https://sainthenribasketball.com",
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plan choice confirmation email failed for {UserId} and season {SeasonId}", user.Id, season.Id);
        }
    }

    private async Task<SeasonPlanStateDto> BuildStateAsync(
        Guid seasonId, string name, DateTime start, DateTime end,
        decimal price, int capacity, Guid userId)
    {
        // Both callers build state for the season the club is currently selling, so a profile still
        // set to the season plan counts here — see GetSpotHolderIdsAsync.
        var holders = await _choices.GetSpotHolderIdsAsync(seasonId, includeProfilePlan: true);
        var taken = holders.Count;
        var paid = await _choices.CountPaidPassesAsync(seasonId);
        var mine = await _choices.GetAsync(seasonId, userId);
        var minePaid = await _choices.HasPaidPassAsync(seasonId, userId);

        return new SeasonPlanStateDto
        {
            SeasonId = seasonId,
            SeasonName = name,
            StartDate = start,
            EndDate = end,
            Price = price,
            Capacity = capacity,
            SpotsTaken = taken,
            // Never negative: capacity can be lowered below the number already sold.
            SpotsLeft = Math.Max(0, capacity - taken),
            SoldOut = taken >= capacity,
            SpotsPaid = paid,
            // Spots held by someone who has not paid yet. An admin reset is what frees these.
            SpotsAwaitingPayment = Math.Max(0, taken - paid),
            MyPlan = mine?.Plan,
            MyPlanPaid = minePaid,
        };
    }
}
