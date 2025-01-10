using SaintHenriBasketball.Application.DTOs.Attendance;

namespace SaintHenriBasketball.Application.DTOs.Session;

public class SessionStatsDto
{
    public Guid SessionId { get; set; }
    public DateTime SessionDate { get; set; }
    public int TotalRegistered { get; set; }
    public int TotalPresent { get; set; }
    public double AttendanceRate { get; set; }
    public List<AttendeeDetailDto> Attendees { get; set; }
}
