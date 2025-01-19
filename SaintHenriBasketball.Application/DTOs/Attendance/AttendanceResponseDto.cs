namespace SaintHenriBasketball.Application.DTOs.Attendance;

public class AttendanceResponseDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public required string UserName { get; set; }
    public bool IsAttending { get; set; }
    public DateTime? CheckInTime { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastUpdated { get; set; }
    public string? UpdateReason { get; set; }
}