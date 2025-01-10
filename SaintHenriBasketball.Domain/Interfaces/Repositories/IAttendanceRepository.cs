using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IAttendanceRepository
{
    Task<SessionAttendance?> GetAttendanceAsync(Guid sessionId, Guid userId);
    Task<IEnumerable<SessionAttendance>> GetSessionAttendancesAsync(Guid sessionId);
    Task<IEnumerable<SessionAttendance>> GetUserAttendanceHistoryAsync(Guid userId);
    Task AddAsync(SessionAttendance attendance);
    Task UpdateAsync(SessionAttendance attendance);
}
