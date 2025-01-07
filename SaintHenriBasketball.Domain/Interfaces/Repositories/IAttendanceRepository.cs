using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IAttendanceRepository
{
    Task<SessionAttendance?> GetAttendanceAsync(Guid sessionId, Guid userId);
    Task<IReadOnlyList<SessionAttendance>> GetSessionAttendancesAsync(Guid sessionId);
    Task<IReadOnlyList<SessionAttendance>> GetUserAttendancesAsync(Guid userId);
    Task<IReadOnlyList<SessionAttendance>> GetAttendancesByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task AddAsync(SessionAttendance attendance);
    Task UpdateAsync(SessionAttendance attendance);
}
