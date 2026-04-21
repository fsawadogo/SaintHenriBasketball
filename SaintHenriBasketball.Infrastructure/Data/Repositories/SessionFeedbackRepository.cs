using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SessionFeedbackRepository : ISessionFeedbackRepository
{
    private readonly ApplicationDbContext _context;

    public SessionFeedbackRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SessionFeedback feedback)
    {
        await _context.SessionFeedbacks.AddAsync(feedback);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid sessionId, Guid userId) =>
        await _context.SessionFeedbacks.AnyAsync(f => f.SessionId == sessionId && f.UserId == userId);

    public async Task<IReadOnlyList<SessionFeedback>> GetBySessionAsync(Guid sessionId) =>
        await _context.SessionFeedbacks
            .AsNoTracking()
            .Where(f => f.SessionId == sessionId)
            .OrderByDescending(f => f.CreatedOn)
            .ToListAsync();
}
