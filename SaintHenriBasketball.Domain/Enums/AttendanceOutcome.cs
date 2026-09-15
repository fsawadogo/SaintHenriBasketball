namespace SaintHenriBasketball.Domain.Enums;

/// What actually happened at a session, recorded by a court captain or admin.
/// Separate from SessionAttendance.IsAttending, which is the player's own answer.
public enum AttendanceOutcome
{
    Unmarked = 0,
    Attended = 1,
    NoShow = 2,
    WalkIn = 3,
}
