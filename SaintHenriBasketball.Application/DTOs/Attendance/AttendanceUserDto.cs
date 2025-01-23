namespace SaintHenriBasketball.Application.DTOs.Attendance;

public class AttendanceUserDto
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAttending { get; set; }
    public DateTime? CheckInTime { get; set; }
    public string? Notes { get; set; }
}