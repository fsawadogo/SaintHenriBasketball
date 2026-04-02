namespace SaintHenriBasketball.Application.DTOs.Email;

public class ScheduleEmailRequestDto
{
    public required List<string> Emails { get; set; }
    public required string Subject { get; set; }
    public required string Message { get; set; }
    public DateTime ScheduledAt { get; set; }
}
