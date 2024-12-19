using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs;

public class PlayerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class CreatePlayerDto
{
    public string Name { get; set; }
    public string Email { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
}

public class UpdatePlayerDto
{
    public string Name { get; set; }
    public string Email { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
}