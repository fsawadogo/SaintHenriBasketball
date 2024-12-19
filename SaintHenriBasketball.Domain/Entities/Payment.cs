using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid PlayerId { get; private set; }
    public Guid? SessionId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public Player Player { get; private set; }
    public TrainingSession Session { get; private set; }

    private Payment() { } // For EF Core

    public Payment(Guid playerId, decimal amount, Guid? sessionId = null)
    {
        Id = Guid.NewGuid();
        PlayerId = playerId;
        SessionId = sessionId;
        Amount = amount;
        Status = PaymentStatus.Pending;
        CreatedOn = DateTime.UtcNow;
    }

    public void UpdateStatus(PaymentStatus newStatus)
    {
        Status = newStatus;
        ModifiedOn = DateTime.UtcNow;
    }
}
