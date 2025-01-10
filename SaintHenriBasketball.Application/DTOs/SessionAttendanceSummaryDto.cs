namespace SaintHenriBasketball.Application.DTOs;

public class SessionAttendanceSummaryDto
{
    public Guid SessionId { get; set; }
    public DateTime SessionDate { get; set; }
    public int TotalAttendees { get; set; }
    public int RegisteredCount { get; set; }
    public int ConfirmedCount { get; set; }
    public decimal AttendanceRate { get; set; }
    public List<AttendanceResponseDto> Attendances { get; set; }
}