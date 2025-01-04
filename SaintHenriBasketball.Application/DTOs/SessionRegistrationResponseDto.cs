using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs;

public class SessionRegistrationResponseDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
    public DateTime RegistrationDate { get; set; }
}
