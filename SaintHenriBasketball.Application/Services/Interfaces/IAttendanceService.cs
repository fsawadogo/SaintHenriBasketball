using SaintHenriBasketball.Application.DTOs;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IAttendanceService
{
    Task<AttendanceDto> MarkAttendanceAsync(Guid sessionId, Guid userId, bool isPresent, string? notes);
    Task<SessionAttendanceSummaryDto> GetSessionAttendanceSummaryAsync(Guid sessionId);
    Task<IReadOnlyList<AttendanceDto>> GetUserAttendanceHistoryAsync(Guid userId);
    Task<AttendanceStatsDto> GetAttendanceStatsAsync(DateTime startDate, DateTime endDate);
}
