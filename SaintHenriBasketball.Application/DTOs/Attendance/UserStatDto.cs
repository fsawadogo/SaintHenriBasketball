namespace SaintHenriBasketball.Application.DTOs.Attendance;

public class UserStatDto
{
    public Guid UserId { get; set; }
    public required string UserName { get; set; }
    public int TotalSessions { get; set; }
    public int AttendedSessions { get; set; }
    public double AttendanceRate { get; set; }
}
