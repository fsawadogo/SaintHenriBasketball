using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SeasonRolloverRepository : ISeasonRolloverRepository
{
    private readonly ApplicationDbContext _context;

    public SeasonRolloverRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Season>> GetOverlappingSeasonsAsync(DateTime startDate, DateTime endDate)
    {
        var from = startDate.Date;
        var toExclusive = endDate.Date.AddDays(1);
        return await _context.Seasons.AsNoTracking()
            .Where(s => s.StartDate < toExclusive && s.EndDate >= from)
            .OrderBy(s => s.StartDate)
            .ToListAsync();
    }

    public Task<bool> SeasonExistsWithDatesAsync(DateTime startDate, DateTime endDate)
    {
        // Compare calendar dates so a season stored with a time of day still counts as the same season.
        var start = startDate.Date;
        var startNext = start.AddDays(1);
        var end = endDate.Date;
        var endNext = end.AddDays(1);
        return _context.Seasons.AnyAsync(s =>
            s.StartDate >= start && s.StartDate < startNext && s.EndDate >= end && s.EndDate < endNext);
    }

    public async Task<IReadOnlyList<Session>> GetSessionsBetweenAsync(DateTime fromDate, DateTime toDate)
    {
        var from = fromDate.Date;
        var toExclusive = toDate.Date.AddDays(1);
        return await _context.Sessions.AsNoTracking()
            .Where(s => s.SessionDate >= from && s.SessionDate < toExclusive)
            .OrderBy(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetPlayersWithCompletedSeasonPaymentAsync(Guid seasonId) =>
        await _context.Users.AsNoTracking()
            .Where(u => _context.Payments.Any(p => p.UserId == u.Id && p.SeasonId == seasonId && p.Status == PaymentStatus.Completed))
            .ToListAsync();

    public async Task<IReadOnlyList<ApplicationUser>> GetSeasonPlanPlayersAsync() =>
        await _context.Users.AsNoTracking()
            .Where(u => u.PaymentPlan == PaymentPlan.Season)
            .ToListAsync();

    public async Task<IReadOnlyList<Guid>> GetUserIdsWithSeasonPaymentAsync(Guid seasonId) =>
        await _context.Payments.AsNoTracking()
            .Where(p => p.SeasonId == seasonId && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Completed))
            .Select(p => p.UserId)
            .Distinct()
            .ToListAsync();
}
