using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Users;

public class UpdateUserDto
{
    public required string? Username { get; set; }
    public required string? Email { get; set; }
    public required string? FirstName { get; set; }
    public required string? LastName { get; set; }
    /// Left unchanged when omitted.
    public PaymentPlan? PaymentPlan { get; set; }
    
    /// Ignored: admin access changes go through PUT Users/{id}/admin.
    public bool IsAdmin { get; set; }
}
