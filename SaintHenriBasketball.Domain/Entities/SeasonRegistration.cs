namespace SaintHenriBasketball.Domain.Entities;

public class SeasonRegistration
{
    public Guid Id { get; private set; }
    public Guid SeasonId { get; set; }
    public Guid UserId { get; set; }
    public DateTime RegisteredOn { get; private set; }

    public virtual Season Season { get; set; }
    public virtual ApplicationUser User { get; set; }

    private SeasonRegistration() { } // For EF Core

    public SeasonRegistration(Guid seasonId, Guid userId)
    {
        Id = Guid.NewGuid();
        SeasonId = seasonId;
        UserId = userId;
        RegisteredOn = DateTime.UtcNow;
    }
}