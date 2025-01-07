namespace SaintHenriBasketball.Domain.Entities;

public class SessionAttendance
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public bool IsPresent { get; set; }
    public DateTime? CheckInTime { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedOn { get; set; }

    public virtual Session Session { get; set; }
    public virtual ApplicationUser User { get; set; }
}
