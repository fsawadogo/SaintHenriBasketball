using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
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
    private readonly ISeasonRepository _seasons;
    private readonly ISeasonPlanChoiceRepository _choices;
    private readonly IUserRepository _users;
    private readonly IAuditLogService _audit;
    private readonly ILogger<SeasonPlanService> _logger;

    public SeasonPlanService(
        ISeasonRepository seasons,
        ISeasonPlanChoiceRepository choices,
        IUserRepository users,
        IAuditLogService audit,
        ILogger<SeasonPlanService> logger)
    {
        _seasons = seasons;
        _choices = choices;
        _users = users;
        _audit = audit;
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

        if (plan == PaymentPlan.Season)
        {
            // A player who has already paid may always re-affirm their own pass, even at capacity —
            // otherwise a second click, or a reset, could lock someone out of something they bought.
            var alreadyPaid = await _choices.HasPaidPassAsync(season.Id, userId);
            // The same count the page shows, so a season that reads as full cannot still be joined.
            var taken = (await _choices.GetSpotHolderIdsAsync(season.Id, includeProfilePlan: true)).Count;
            if (!alreadyPaid && taken >= season.SeasonPassCapacity)
                throw new ValidationException("The season pass is sold out for this season.");
        }

        await _choices.UpsertAsync(season.Id, userId, plan);

        // Keep the denormalised current plan coherent: everything else in the app still reads it.
        var user = await _users.GetByIdAsync(userId);
        if (user is not null && user.PaymentPlan != plan)
        {
            user.PaymentPlan = plan;
            await _users.UpdateAsync(user);
        }

        _logger.LogInformation("User {UserId} chose {Plan} for season {SeasonId}", userId, plan, season.Id);

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
