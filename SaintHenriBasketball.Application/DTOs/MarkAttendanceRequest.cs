namespace SaintHenriBasketball.Application.DTOs;

public class MarkAttendanceRequest
{
    public bool IsAttending { get; set; }
    public string? Notes { get; set; }
}
