using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SessionRegistrationRepository : GenericRepository<SessionRegistration>, ISessionRegistrationRepository
{
    public SessionRegistrationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<SessionRegistration> GetRegistrationByPlayerAndSessionAsync(Guid playerId, Guid sessionId)
    {
        return await _context.SessionRegistrations
            .Include(sr => sr.Player)
            .Include(sr => sr.Session)
            .FirstOrDefaultAsync(sr => sr.PlayerId == playerId && sr.SessionId == sessionId);
    }

    public async Task<IReadOnlyList<SessionRegistration>> GetRegistrationsByPlayerAsync(Guid playerId)
    {
        return await _context.SessionRegistrations
            .Include(sr => sr.Session)
            .Where(sr => sr.PlayerId == playerId)
            .OrderBy(sr => sr.Session.SessionDate)
            .ToListAsync();
    }
}
