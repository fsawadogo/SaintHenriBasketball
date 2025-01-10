namespace SaintHenriBasketball.Application.DTOs.Attendance;

public class SessionStatDto
{
    public Guid SessionId { get; set; }
    public DateTime Date { get; set; }
    public int TotalPresent { get; set; }
    public double AttendanceRate { get; set; }
}

