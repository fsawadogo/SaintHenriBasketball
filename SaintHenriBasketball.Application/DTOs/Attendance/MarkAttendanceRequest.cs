namespace SaintHenriBasketball.Application.DTOs.Attendance;

public class MarkAttendanceRequest
{
    public bool IsAttending { get; set; }
    public string? Notes { get; set; }
}
