using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// One player on a session roster. IsAttending is null when the player never answered.
/// CheckInTime is UTC.
public record CourtRosterEntry(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Email,
    PaymentPlan PaymentPlan,
    bool IsDeactivated,
    bool? IsAttending,
    AttendanceOutcome Outcome,
    DateTime? CheckInTime);

/// One (player, session) roster place in a no-show window. SessionDate, StartTime and EndTime are Montreal local.
public record CourtOutcomeRow(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Email,
    bool IsDeactivated,
    Guid SessionId,
    DateTime SessionDate,
    string StartTime,
    string EndTime,
    AttendanceOutcome Outcome);

/// Read side of court attendance. A player is on a session's roster when they hold a registration, or their
/// attendance row says they're coming or already has an outcome (walk-ins and older rows without a registration).
public interface ICourtAttendanceRepository
{
    /// Everyone on the session's roster, ordered by last name then first name.
    Task<IReadOnlyList<CourtRosterEntry>> GetRosterAsync(Guid sessionId);

    /// Roster places for sessions dated from <paramref name="sessionDateFrom"/> to <paramref name="sessionDateTo"/>
    /// (inclusive calendar dates), excluding cancelled sessions. Optionally one player only.
    Task<IReadOnlyList<CourtOutcomeRow>> GetOutcomeRowsAsync(DateTime sessionDateFrom, DateTime sessionDateTo, Guid? userId);
}
