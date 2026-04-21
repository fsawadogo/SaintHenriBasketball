using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Users;

public class UserDto
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsAdmin { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
    public DateTime CreatedOn { get; set; }
    public EmailLanguage PreferredLanguage { get; set; }
    public bool TwoFactorEnabled { get; set; }
}