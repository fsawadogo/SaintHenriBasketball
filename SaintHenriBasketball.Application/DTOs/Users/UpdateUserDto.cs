using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Users;

public class UpdateUserDto
{
    public required string? Username { get; set; }
    public required string? Email { get; set; }
    public required string? FirstName { get; set; }
    public required string? LastName { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
    
    public bool IsAdmin { get; set; }
}
