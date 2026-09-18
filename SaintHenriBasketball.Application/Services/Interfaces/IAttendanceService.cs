using SaintHenriBasketball.Application.DTOs.Attendance;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IAttendanceService
{
    Task<AttendanceResponseDto> MarkAttendanceAsync(Guid sessionId, Guid userId, bool isAttending, string? notes);
    Task<AttendanceResponseDto> UpdateAttendanceAsync(Guid sessionId, Guid userId, bool isAttending, string? notes = null, string? updateReason = null);
    Task<IEnumerable<AttendanceResponseDto>> GetUserAttendanceHistoryAsync(Guid userId);
    Task<SessionAttendanceSummaryDto> GetSessionAttendanceSummaryAsync(Guid sessionId);
    Task<IEnumerable<AttendanceUserDto>> GetSessionAttendeesAsync(Guid sessionId);

    /// Everyone registered for the session, with whether they have confirmed. Not cached: this is
    /// read right after someone books, and a stale roster reads as a player who vanished.
    Task<IReadOnlyList<Domain.Interfaces.Repositories.SessionRosterEntry>> GetSessionRosterAsync(Guid sessionId);
    Task<AddParticipantsResponseDto> AddParticipantsToSessionAsync(Guid sessionId, AddParticipantsRequest request);
    Task<RemoveParticipantsResponseDto> RemoveParticipantsFromSessionAsync(Guid sessionId, RemoveParticipantsRequest request);

}