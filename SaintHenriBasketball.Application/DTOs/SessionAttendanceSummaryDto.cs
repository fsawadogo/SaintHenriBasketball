namespace SaintHenriBasketball.Application.DTOs;

public class SessionAttendanceSummaryDto
{
    public Guid SessionId { get; set; }
    public DateTime SessionDate { get; set; }
    public int TotalRegistered { get; set; }
    public int TotalPresent { get; set; }
    public List<AttendanceDto> Attendances { get; set; }
}