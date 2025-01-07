namespace SaintHenriBasketball.Application.DTOs;

public class AttendanceStatsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalSessions { get; set; }
    public int TotalAttendees { get; set; }
    public double AverageAttendanceRate { get; set; }
    public List<SessionStatsDto> SessionStats { get; set; }
}

