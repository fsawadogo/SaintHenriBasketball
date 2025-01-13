using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Season;

public class SeasonUserDto
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime RegisteredOn { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
    public string Email { get; set; } = string.Empty;
}