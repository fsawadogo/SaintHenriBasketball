using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SeasonScheduleRepository : ISeasonScheduleRepository
{
    private readonly ApplicationDbContext _context;

    public SeasonScheduleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Session>> GetActiveSessionsBetweenAsync(DateTime fromDate, DateTime toDate)
    {
        var from = fromDate.Date;
        var toExclusive = toDate.Date.AddDays(1);
        return await _context.Sessions.AsNoTracking()
            .Where(s => s.SessionDate >= from && s.SessionDate < toExclusive && s.Status != SessionStatus.Cancelled)
            .ToListAsync();
    }

    public async Task AddSeasonWithSessionsAsync(Season season, IReadOnlyList<Session> sessions)
    {
        // One SaveChanges runs every insert inside a single database transaction,
        // so a season is never left without the sessions the admin asked for.
        _context.Seasons.Add(season);
        if (sessions.Count > 0) _context.Sessions.AddRange(sessions);
        await _context.SaveChangesAsync();
    }
}
