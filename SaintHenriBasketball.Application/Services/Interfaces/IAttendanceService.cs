using SaintHenriBasketball.Application.DTOs.Attendance;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IAttendanceService
{
    Task<AttendanceResponseDto> MarkAttendanceAsync(Guid sessionId, Guid userId, bool isAttending, string? notes);
    Task<AttendanceResponseDto> UpdateAttendanceAsync(Guid sessionId, Guid userId, bool isAttending, string? notes = null, string? updateReason = null);
    Task<IEnumerable<AttendanceResponseDto>> GetUserAttendanceHistoryAsync(Guid userId);
    Task<SessionAttendanceSummaryDto> GetSessionAttendanceSummaryAsync(Guid sessionId);
    Task<IEnumerable<AttendanceUserDto>> GetSessionAttendeesAsync(Guid sessionId);

}