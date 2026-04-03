using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

public class Waitlist
{
    public Guid Id { get; private set; }
    public Guid UserId { get; set; }
    public Guid SessionId { get; set; }
    public int Position { get; set; }
    public WaitlistStatus Status { get; set; }
    public DateTime RegistrationDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedOn { get; private set; }

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Session Session { get; set; } = null!;

    private Waitlist() { }

    public Waitlist(Guid userId, Guid sessionId, int position)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        SessionId = sessionId;
        Position = position;
        Status = WaitlistStatus.Waiting;
        RegistrationDate = DateTime.UtcNow;
        CreatedOn = DateTime.UtcNow;
    }
}
