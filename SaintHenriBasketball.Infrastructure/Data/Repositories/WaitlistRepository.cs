using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class WaitlistRepository : IWaitlistRepository
{
    private readonly ApplicationDbContext _context;

    public WaitlistRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Waitlist>> GetBySessionAsync(Guid sessionId)
    {
        return await _context.Waitlists
            .Include(w => w.User)
            .Where(w => w.SessionId == sessionId && w.Status == WaitlistStatus.Waiting)
            .OrderBy(w => w.Position)
            .ToListAsync();
    }

    public async Task<Waitlist?> GetByUserAndSessionAsync(Guid userId, Guid sessionId)
    {
        return await _context.Waitlists
            .FirstOrDefaultAsync(w => w.UserId == userId && w.SessionId == sessionId
                && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Offered));
    }

    public async Task<Waitlist?> GetNextInLineAsync(Guid sessionId)
    {
        return await _context.Waitlists
            .Include(w => w.User)
            .Where(w => w.SessionId == sessionId && w.Status == WaitlistStatus.Waiting)
            .OrderBy(w => w.Position)
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetWaitlistCountAsync(Guid sessionId)
    {
        return await _context.Waitlists
            .CountAsync(w => w.SessionId == sessionId && w.Status == WaitlistStatus.Waiting);
    }

    public async Task AddAsync(Waitlist entry)
    {
        await _context.Waitlists.AddAsync(entry);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Waitlist entry)
    {
        _context.Waitlists.Update(entry);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entry = await _context.Waitlists.FindAsync(id);
        if (entry != null)
        {
            _context.Waitlists.Remove(entry);
            await _context.SaveChangesAsync();
        }
    }
}
