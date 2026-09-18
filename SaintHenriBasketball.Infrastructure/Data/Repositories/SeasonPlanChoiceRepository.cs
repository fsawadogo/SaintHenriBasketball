using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SeasonPlanChoiceRepository : ISeasonPlanChoiceRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SeasonPlanChoiceRepository> _logger;

    public SeasonPlanChoiceRepository(ApplicationDbContext context, ILogger<SeasonPlanChoiceRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SeasonPlanChoice?> GetAsync(Guid seasonId, Guid userId) =>
        await _context.SeasonPlanChoices
            .FirstOrDefaultAsync(c => c.SeasonId == seasonId && c.UserId == userId);

    public async Task<SeasonPlanChoice> UpsertAsync(Guid seasonId, Guid userId, PaymentPlan plan)
    {
        var existing = await _context.SeasonPlanChoices
            .FirstOrDefaultAsync(c => c.SeasonId == seasonId && c.UserId == userId);

        if (existing is not null)
        {
            existing.Plan = plan;
            existing.ChosenOn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return existing;
        }

        var choice = new SeasonPlanChoice(seasonId, userId, plan);
        _context.SeasonPlanChoices.Add(choice);
        await _context.SaveChangesAsync();
        return choice;
    }

    /// Queried straight off Payments rather than through IPaymentRepository: the question is exactly
    /// "Plan == Season && SeasonId == x && Status == Completed", and going through the payment
    /// repository would drag in its caching and its Refunded handling.
    private IQueryable<Guid> PaidPassUserIds(Guid seasonId) =>
        _context.Payments
            .Where(p => p.SeasonId == seasonId
                        && p.Plan == PaymentPlan.Season
                        && p.Status == PaymentStatus.Completed)
            .Select(p => p.UserId)
            .Distinct();

    // Distinct USERS, never payment rows: a player can hold an abandoned Pending Interac row and a
    // completed card row for the same season, and counting rows would sell out a season with seats left.
    public async Task<int> CountPaidPassesAsync(Guid seasonId) =>
        await PaidPassUserIds(seasonId).CountAsync();

    public async Task<IReadOnlyList<Guid>> GetSpotHolderIdsAsync(Guid seasonId, bool includeProfilePlan)
    {
        // Union, not a sum: the same player usually appears in more than one of these, and a paid
        // pass holder almost always has a choice row too.
        var ids = PaidPassUserIds(seasonId)
            .Union(_context.SeasonPlanChoices
                .Where(c => c.SeasonId == seasonId && c.Plan == PaymentPlan.Season)
                .Select(c => c.UserId));

        if (includeProfilePlan)
        {
            // The denormalised plan on the user is the only trace left when an admin switches
            // someone to the season plan without them choosing it in the app.
            ids = ids.Union(_context.Users
                .Where(u => u.PaymentPlan == PaymentPlan.Season && !u.IsDeactivated && !u.IsAdmin)
                .Select(u => u.Id));
        }

        return await ids.Distinct().ToListAsync();
    }

    public async Task<bool> HasPaidPassAsync(Guid seasonId, Guid userId) =>
        await _context.Payments.AnyAsync(p => p.SeasonId == seasonId
                                              && p.UserId == userId
                                              && p.Plan == PaymentPlan.Season
                                              && p.Status == PaymentStatus.Completed);

    public async Task<List<Guid>> GetUnpaidChoiceUserIdsAsync(Guid seasonId)
    {
        var paid = PaidPassUserIds(seasonId);
        return await _context.SeasonPlanChoices
            .Where(c => c.SeasonId == seasonId && !paid.Contains(c.UserId))
            .Select(c => c.UserId)
            .ToListAsync();
    }

    public async Task<int> ClearUnpaidForSeasonAsync(Guid seasonId)
    {
        var paid = await PaidPassUserIds(seasonId).ToListAsync();
        var doomed = await _context.SeasonPlanChoices
            .Where(c => c.SeasonId == seasonId && !paid.Contains(c.UserId))
            .ToListAsync();

        if (doomed.Count == 0) return 0;

        _context.SeasonPlanChoices.RemoveRange(doomed);
        await _context.SaveChangesAsync();
        _logger.LogInformation(
            "Cleared {Cleared} unpaid season plan choices for season {SeasonId}; {Kept} paid passes kept",
            doomed.Count, seasonId, paid.Count);
        return doomed.Count;
    }
}
