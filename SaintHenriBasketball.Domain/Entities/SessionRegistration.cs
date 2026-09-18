using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

public class SessionRegistration
{
    public Guid Id { get; private set; }
    public Guid UserId { get; set; }
    public Guid SessionId { get; set; }
    public DateTime RegistrationDate { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
    /// When the "your spot is booked" email went out, or null if it never did. Lets a confirmation
    /// be sent once and only once, and makes the ones that were missed findable afterwards.
    public DateTime? ConfirmationSentOn { get; set; }
    public ApplicationUser User { get; set; }
    public Session Session { get; set; }

    private SessionRegistration() { } // For EF Core

    public SessionRegistration(Guid userId, Guid sessionId, PaymentPlan paymentPlan)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        SessionId = sessionId;
        RegistrationDate = DateTime.UtcNow;
        PaymentPlan = paymentPlan;
    }
}