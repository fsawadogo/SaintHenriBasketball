using SaintHenriBasketball.Application.DTOs.CourtAttendance;

namespace SaintHenriBasketball.Application.Services.Interfaces;

/// Court attendance: a session roster where a captain or admin records what happened, and no-show stats.
/// Throws NotFoundException for an unknown session, player or roster place; ValidationException for refused changes.
public interface ICourtAttendanceService
{
    Task<SessionRosterDto> GetRosterAsync(Guid sessionId);

    /// Sets Attended, NoShow or Unmarked for a player on the roster and returns their updated roster line.
    Task<RosterPlayerDto> SetOutcomeAsync(Guid sessionId, Guid userId, string? outcome);

    /// Registers an existing player who wasn't on the roster and marks them as a walk-in.
    Task<RosterPlayerDto> AddWalkInAsync(Guid sessionId, Guid userId);

    Task<NoShowStatsPageDto> GetNoShowStatsAsync(NoShowStatsQuery query);
}
