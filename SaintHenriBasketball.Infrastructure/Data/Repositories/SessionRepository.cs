using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SessionRepository : GenericRepository<TrainingSession>, ISessionRepository
{
    public SessionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<TrainingSession> GetSessionWithRegistrationsAsync(Guid id)
    {
        return await _context.TrainingSessions
            .Include(s => s.Registrations)
            .ThenInclude(sr => sr.Player)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IReadOnlyList<TrainingSession>> GetUpcomingSessionsAsync()
    {
        return await _context.TrainingSessions
            .Include(s => s.Registrations)
            .Where(s => s.SessionDate > DateTime.UtcNow && s.Status == SessionStatus.Scheduled)
            .OrderBy(s => s.SessionDate)
            .ToListAsync();
    }

    public async Task<bool> IsSessionAvailableAsync(Guid sessionId)
    {
        var session = await _context.TrainingSessions
            .Include(s => s.Registrations)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        return session?.CanRegister() ?? false;
    }
}