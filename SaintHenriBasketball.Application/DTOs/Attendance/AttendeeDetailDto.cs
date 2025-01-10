namespace SaintHenriBasketball.Application.DTOs.Attendance;

public class AttendeeDetailDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool IsPresent { get; set; }
    public DateTime? CheckInTime { get; set; }
    public string PaymentPlan { get; set; }
    public string? Notes { get; set; }
}
