using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class AttendanceRepository : IAttendanceRepository
{
    private readonly ApplicationDbContext _context;

    public AttendanceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SessionAttendance?> GetAttendanceAsync(Guid sessionId, Guid userId)
    {
        return await _context.SessionAttendances
            .Include(a => a.User)
            .Include(a => a.Session)
            .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.UserId == userId);
    }

    public async Task<IReadOnlyList<SessionAttendance>> GetSessionAttendancesAsync(Guid sessionId)
    {
        return await _context.SessionAttendances
            .Include(a => a.User)
            .Include(a => a.Session)
            .Where(a => a.SessionId == sessionId)
            .OrderBy(a => a.User.Username)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<SessionAttendance>> GetUserAttendancesAsync(Guid userId)
    {
        return await _context.SessionAttendances
            .Include(a => a.User)
            .Include(a => a.Session)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Session.SessionDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<SessionAttendance>> GetAttendancesByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.SessionAttendances
            .Include(a => a.User)
            .Include(a => a.Session)
            .Where(a => a.Session.SessionDate >= startDate && a.Session.SessionDate <= endDate)
            .OrderByDescending(a => a.Session.SessionDate)
            .ToListAsync();
    }

    public async Task AddAsync(SessionAttendance attendance)
    {
        await _context.SessionAttendances.AddAsync(attendance);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SessionAttendance attendance)
    {
        _context.SessionAttendances.Update(attendance);
        await _context.SaveChangesAsync();
    }
}