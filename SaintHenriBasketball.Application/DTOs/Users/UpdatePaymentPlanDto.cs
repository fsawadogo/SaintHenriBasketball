using SaintHenriBasketball.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SaintHenriBasketball.Application.DTOs.Users;

public class UpdatePaymentPlanDto
{
    [Required(ErrorMessage = "Payment plan is required")]
    public PaymentPlan PaymentPlan { get; set; }
}
