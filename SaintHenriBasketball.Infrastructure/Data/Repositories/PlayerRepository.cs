using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class PlayerRepository : GenericRepository<Player>, IPlayerRepository
{
    public PlayerRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Player> GetPlayerWithRegistrationsAsync(Guid id)
    {
        return await _context.Players
            .Include(p => p.SessionRegistrations)
            .ThenInclude(sr => sr.Session)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Player> GetPlayerByEmailAsync(string email)
    {
        return await _context.Players
            .FirstOrDefaultAsync(p => p.Email == email);
    }

    public async Task<IReadOnlyList<Player>> GetPlayersBySessionAsync(Guid sessionId)
    {
        return await _context.Players
            .Include(p => p.SessionRegistrations)
            .Where(p => p.SessionRegistrations.Any(sr => sr.SessionId == sessionId))
            .ToListAsync();
    }
}
