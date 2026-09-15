using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IParticipationRepository
{
    Task<SessionRegistration> ReserveAsync(Guid sessionId, Guid userId);
    Task CancelAsync(Guid sessionId, Guid userId);
    Task<SessionAttendance> CheckInAsync(Guid sessionId, Guid userId);
    Task LeaveWaitlistAsync(Guid sessionId, Guid userId);
    Task<SessionAttendance> SetAttendanceAsync(Guid sessionId, Guid userId, bool attending, string? notes, string? reason);
    Task<Waitlist> JoinWaitlistAsync(Guid sessionId, Guid userId, string? notes);
    Task<Waitlist?> OfferNextAsync(Guid sessionId);
    Task<IReadOnlyList<Guid>> GetWaitlistSessionIdsAsync();

    // Court attendance (admin at the court). Same session lock, registration and attendance row as the admin
    // add-participant path, but still allowed after the session starts, when walk-ins arrive and outcomes are known.

    /// Sets what happened for a player on the roster. Creates the attendance row for a registered player who never
    /// answered; never changes IsAttending on an existing row. Attended fills in a missing CheckInTime.
    Task<SessionAttendance> SetOutcomeAsync(Guid sessionId, Guid userId, AttendanceOutcome outcome, string reason);

    /// Registers an active player who isn't on the roster and marks them WalkIn with a check-in time.
    /// Refused at capacity; the waitlist doesn't block it (the player's own waitlist entry is accepted).
    Task<SessionAttendance> AddWalkInAsync(Guid sessionId, Guid userId, string reason);
}
