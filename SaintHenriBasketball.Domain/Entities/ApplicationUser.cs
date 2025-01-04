using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Domain.Entities;

public class ApplicationUser
{
    public Guid Id { get; private set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; private set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public bool IsAdmin { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
    public DateTime CreatedOn { get; private set; }
    public ICollection<SessionRegistration> SessionRegistrations { get; private set; }

    private ApplicationUser() { } // For EF Core

    public ApplicationUser(string username, string email, string passwordHash, string firstName, string lastName, PaymentPlan paymentPlan)
    {
        Id = Guid.NewGuid();
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        PaymentPlan = paymentPlan;
        CreatedOn = DateTime.UtcNow;
        IsAdmin = false;
        SessionRegistrations = new List<SessionRegistration>();
    }
}