namespace SaintHenriBasketball.Application.DTOs;

public class UpdateAttendanceRequest
{
    public bool IsAttending { get; set; }
    public string? Notes { get; set; }
    public string? UpdateReason { get; set; }
}