using System.ComponentModel.DataAnnotations;
namespace SaintHenriBasketball.Application.DTOs.Email;

public class ScheduleEmailRequestDto
{
    [MaxLength(EmailRecipientLimits.MaxRecipients, ErrorMessage = "Send to at most 2000 email addresses at a time.")]
    public required List<string> Emails { get; set; }
    public required string Subject { get; set; }
    public required string Message { get; set; }
    public DateTime ScheduledAt { get; set; }
}
