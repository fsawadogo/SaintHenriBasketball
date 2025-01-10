using SaintHenriBasketball.Application.DTOs.Session;

namespace SaintHenriBasketball.Application.DTOs.Attendance;

public class AttendanceStatsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalSessions { get; set; }
    public double AverageAttendance { get; set; }
    public List<SessionStatDto> SessionStats { get; set; } = new();
    public List<UserStatDto> UserStats { get; set; } = new();
}
