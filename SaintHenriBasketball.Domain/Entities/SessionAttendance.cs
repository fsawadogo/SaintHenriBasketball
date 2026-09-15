using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

public class SessionAttendance
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public DateTime? CheckInTime { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedOn { get; set; }

    public virtual Session Session { get; set; }
    public virtual ApplicationUser User { get; set; }
    public bool IsAttending { get; set; }
    public string? UpdateReason { get; set; }
    public DateTime LastUpdated { get; set; }

    /// What happened at the session (court attendance). IsAttending stays the player's own answer.
    public AttendanceOutcome Outcome { get; set; }
}
