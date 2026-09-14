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

    public Guid? SeasonId { get; set; }
    public Season? Season { get; set; }

    /// <summary>
    /// List price before any promo discount or account credit. Null on payments that were never
    /// adjusted; read it as <c>OriginalAmount ?? Amount</c>. <see cref="Amount"/> is always what is
    /// charged: OriginalAmount - DiscountAmount - CreditApplied.
    /// </summary>
    public decimal? OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal CreditApplied { get; set; }
    public Guid? PromoCodeId { get; set; }
    public PromoCode? PromoCode { get; set; }

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