namespace SaintHenriBasketball.Domain.Entities;

public class ApplicationUser : BaseEntity
{
    public string Username { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public bool IsAdmin { get; private set; }

    private ApplicationUser() { } // For EF Core

    public ApplicationUser(string username, string email, string passwordHash, string firstName, string lastName, bool isAdmin = false)
    {
        Id = Guid.NewGuid();
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        IsAdmin = isAdmin;
        CreatedOn = DateTime.UtcNow;
    }

    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
        ModifiedOn = DateTime.UtcNow;
    }
}