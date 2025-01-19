namespace SaintHenriBasketball.Application.DTOs.Attendance;

public class AttendeeDetailDto
{
    public Guid UserId { get; set; }
    public required string UserName { get; set; }
    public required string Email { get; set; }
    public bool IsAttending { get; set; }
    public DateTime? CheckInTime { get; set; }
    public required string PaymentPlan { get; set; }
    public string? Notes { get; set; }
}
