using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

public class Payment
{
    public Guid Id { get; private set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public PaymentPlan Plan { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime PaymentDate { get; set; }
    public ApplicationUser User { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; }

    // Nullable: only auto-billed drop-in payments are tied to a specific session.
    // Existing season payments and pre-migration rows leave it null.
    public Guid? SessionId { get; set; }
    public Session? Session { get; set; }

    private Payment() { } // For EF Core

    public Payment(Guid userId, decimal amount, PaymentPlan plan)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Amount = amount;
        Plan = plan;
        Status = PaymentStatus.Pending;
        PaymentDate = DateTime.UtcNow;
    }

    public Payment(Guid userId, decimal amount, PaymentPlan plan, Guid sessionId)
        : this(userId, amount, plan)
    {
        SessionId = sessionId;
    }
}