
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

public class Player : BaseEntity
{
    public string Name { get; private set; }
    public string Email { get; private set; }
    public PaymentPlan PaymentPlan { get; private set; }
    private readonly List<SessionRegistration> _sessionRegistrations = new();
    private readonly List<Payment> _payments = new();

    public IReadOnlyCollection<SessionRegistration> SessionRegistrations => _sessionRegistrations.AsReadOnly();
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    private Player() { } // For EF Core

    public Player(string name, string email, PaymentPlan paymentPlan)
    {
        Id = Guid.NewGuid();
        Name = name;
        Email = email;
        PaymentPlan = paymentPlan;
        CreatedOn = DateTime.UtcNow;
    }

    public void UpdatePaymentPlan(PaymentPlan newPlan)
    {
        PaymentPlan = newPlan;
        ModifiedOn = DateTime.UtcNow;
    }

    public void AddSessionRegistration(SessionRegistration registration)
    {
        _sessionRegistrations.Add(registration);
        ModifiedOn = DateTime.UtcNow;
    }
}