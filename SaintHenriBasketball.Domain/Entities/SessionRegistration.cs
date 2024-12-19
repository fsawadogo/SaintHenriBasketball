using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

public class SessionRegistration : BaseEntity
{
    public Guid PlayerId { get; private set; }
    public Guid SessionId { get; private set; }
    public RegistrationStatus Status { get; private set; }
    public Player Player { get; private set; }
    public TrainingSession Session { get; private set; }

    private SessionRegistration() { } // For EF Core

    public SessionRegistration(Guid playerId, Guid sessionId)
    {
        Id = Guid.NewGuid();
        PlayerId = playerId;
        SessionId = sessionId;
        Status = RegistrationStatus.Pending;
        CreatedOn = DateTime.UtcNow;
    }

    public void UpdateStatus(RegistrationStatus newStatus)
    {
        Status = newStatus;
        ModifiedOn = DateTime.UtcNow;
    }
}
