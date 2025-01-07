using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs;

public class AttendanceFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public PaymentPlan? PaymentPlan { get; set; }
    public bool? IsPresent { get; set; }
}
