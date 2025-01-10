using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Session;

public class SessionRegistrationDto
{
    public Guid SessionId { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
}
