using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SessionAttendanceRepository : ISessionAttendanceRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SessionAttendanceRepository> _logger;

    public SessionAttendanceRepository(
        ApplicationDbContext context,
        ILogger<SessionAttendanceRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SessionAttendance?> GetAttendanceAsync(Guid sessionId, Guid userId)
    {
        try
        {
            return await _context.SessionAttendances
                .Include(a => a.User)
                .Include(a => a.Session)
                .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.UserId == userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attendance for session {SessionId} and user {UserId}",
                sessionId, userId);
            throw;
        }
    }

    public async Task<IEnumerable<SessionAttendance>> GetSessionAttendancesAsync(Guid sessionId)
    {
        try
        {
            return await _context.SessionAttendances
                .Include(a => a.User)
                .Include(a => a.Session)
                .Where(a => a.SessionId == sessionId)
                .OrderBy(a => a.User.LastName)
                .ThenBy(a => a.User.FirstName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attendances for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<IEnumerable<SessionAttendance>> GetUserAttendanceHistoryAsync(Guid userId)
    {
        try
        {
            return await _context.SessionAttendances
                .Include(a => a.User)
                .Include(a => a.Session)
                .Where(a => a.UserId == userId && a.Session.Status != SessionStatus.Cancelled)
                .OrderByDescending(a => a.Session.SessionDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attendance history for user {UserId}", userId);
            throw;
        }
    }

    public async Task AddAsync(SessionAttendance attendance)
    {
        try
        {
            await _context.SessionAttendances.AddAsync(attendance);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Added attendance record for session {SessionId} and user {UserId}",
                attendance.SessionId, attendance.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding attendance for session {SessionId} and user {UserId}",
                attendance.SessionId, attendance.UserId);
            throw;
        }
    }

    public async Task UpdateAsync(SessionAttendance attendance)
    {
        try
        {
            _context.SessionAttendances.Update(attendance);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated attendance record for session {SessionId} and user {UserId}",
                attendance.SessionId, attendance.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating attendance for session {SessionId} and user {UserId}",
                attendance.SessionId, attendance.UserId);
            throw;
        }
    }

    public async Task<IEnumerable<SessionAttendance>> GetAttendancesByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            return await _context.SessionAttendances
                .Include(a => a.User)
                .Include(a => a.Session)
                .Where(a => a.Session.SessionDate >= startDate && a.Session.SessionDate <= endDate)
                .OrderBy(a => a.Session.SessionDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attendances for date range {StartDate} to {EndDate}",
                startDate, endDate);
            throw;
        }
    }

    public async Task<(int Total, int Present, int Absent)> GetSessionAttendanceStatsAsync(Guid sessionId)
    {
        try
        {
            var attendances = await _context.SessionAttendances
                .Where(a => a.SessionId == sessionId)
                .ToListAsync();

            int total = attendances.Count;
            int present = attendances.Count(a => a.IsAttending);
            int absent = total - present;

            return (total, present, absent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attendance stats for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<(int Total, int Present, int Absent)> GetUserAttendanceStatsAsync(Guid userId)
    {
        try
        {
            var attendances = await _context.SessionAttendances
                .Where(a => a.UserId == userId)
                .ToListAsync();

            int total = attendances.Count;
            int present = attendances.Count(a => a.IsAttending);
            int absent = total - present;

            return (total, present, absent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attendance stats for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> HasUserMarkedAttendanceAsync(Guid sessionId, Guid userId)
    {
        try
        {
            return await _context.SessionAttendances
                .AnyAsync(a => a.SessionId == sessionId && a.UserId == userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} has marked attendance for session {SessionId}",
                userId, sessionId);
            throw;
        }
    }

    public async Task DeleteAsync(Guid sessionId, Guid userId)
    {
        try
        {
            var attendance = await _context.SessionAttendances
                .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.UserId == userId);

            if (attendance != null)
            {
                _context.SessionAttendances.Remove(attendance);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted attendance record for session {SessionId} and user {UserId}",
                    sessionId, userId);
            }
            else
            {
                _logger.LogWarning("Attempted to delete non-existent attendance record for session {SessionId} and user {UserId}",
                    sessionId, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting attendance for session {SessionId} and user {UserId}",
                sessionId, userId);
            throw;
        }
    }
}