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

    /// Admin: offers a place to one specific waiting entry, exactly like automatic promotion (same status, same
    /// expiry). HadOpenSpot is false when bookings plus outstanding offers already filled the session.
    Task<(Waitlist Entry, bool HadOpenSpot)> OfferEntryAsync(Guid entryId);
    /// Admin: deletes a waiting or offered entry and renumbers the rest of the line 1..N.
    Task<Waitlist> RemoveWaitlistEntryAsync(Guid entryId);
    /// Admin: moves a waiting or offered entry to a 1-based position (clamped) and renumbers; returns the position used.
    Task<(Guid SessionId, int Position)> MoveWaitlistEntryAsync(Guid entryId, int position);
    // Court attendance (admin at the court). Same session lock, registration and attendance row as the admin
    // add-participant path, but still allowed after the session starts, when walk-ins arrive and outcomes are known.

    /// Sets what happened for a player on the roster. Creates the attendance row for a registered player who never
    /// answered; never changes IsAttending on an existing row. Attended fills in a missing CheckInTime.
    Task<SessionAttendance> SetOutcomeAsync(Guid sessionId, Guid userId, AttendanceOutcome outcome, string reason);

    /// Registers an active player who isn't on the roster and marks them WalkIn with a check-in time.
    /// Refused at capacity; the waitlist doesn't block it (the player's own waitlist entry is accepted).
    Task<SessionAttendance> AddWalkInAsync(Guid sessionId, Guid userId, string reason);
}
