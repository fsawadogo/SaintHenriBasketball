using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface ISessionAttendanceRepository
{
    /// <summary>
    /// Gets attendance record for a specific user and session
    /// </summary>
    Task<SessionAttendance?> GetAttendanceAsync(Guid sessionId, Guid userId);

    /// <summary>
    /// Gets all attendance records for a session
    /// </summary>
    Task<IEnumerable<SessionAttendance>> GetSessionAttendancesAsync(Guid sessionId);

    /// <summary>
    /// Gets attendance history for a specific user
    /// </summary>
    Task<IEnumerable<SessionAttendance>> GetUserAttendanceHistoryAsync(Guid userId);

    /// <summary>
    /// Adds a new attendance record
    /// </summary>
    Task AddAsync(SessionAttendance attendance);

    /// <summary>
    /// Updates an existing attendance record
    /// </summary>
    Task UpdateAsync(SessionAttendance attendance);

    /// <summary>
    /// Gets attendance records for a date range
    /// </summary>
    Task<IEnumerable<SessionAttendance>> GetAttendancesByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets attendance statistics for a session
    /// </summary>
    Task<(int Total, int Present, int Absent)> GetSessionAttendanceStatsAsync(Guid sessionId);

    /// <summary>
    /// Gets attendance statistics for a user
    /// </summary>
    Task<(int Total, int Present, int Absent)> GetUserAttendanceStatsAsync(Guid userId);

    /// <summary>
    /// Checks if a user has already marked attendance for a session
    /// </summary>
    Task<bool> HasUserMarkedAttendanceAsync(Guid sessionId, Guid userId);

    /// <summary>
    /// Deletes an attendance record
    /// </summary>
    Task DeleteAsync(Guid sessionId, Guid userId);
}