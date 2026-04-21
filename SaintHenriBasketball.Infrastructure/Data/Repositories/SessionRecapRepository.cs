using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SessionRecapRepository : ISessionRecapRepository
{
    private readonly ApplicationDbContext _context;

    public SessionRecapRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SessionRecap recap)
    {
        await _context.SessionRecaps.AddAsync(recap);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid recapId)
    {
        var recap = await _context.SessionRecaps.FirstOrDefaultAsync(r => r.Id == recapId);
        if (recap is null) return;
        _context.SessionRecaps.Remove(recap);
        await _context.SaveChangesAsync();
    }

    public async Task<SessionRecap?> GetByIdAsync(Guid id) =>
        await _context.SessionRecaps.FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IReadOnlyList<SessionRecap>> GetBySessionAsync(Guid sessionId) =>
        await _context.SessionRecaps
            .AsNoTracking()
            .Where(r => r.SessionId == sessionId)
            .OrderByDescending(r => r.CreatedOn)
            .ToListAsync();

    public async Task<IReadOnlyList<SessionRecap>> GetRecentAsync(int take) =>
        await _context.SessionRecaps
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedOn)
            .Take(take)
            .ToListAsync();
}
