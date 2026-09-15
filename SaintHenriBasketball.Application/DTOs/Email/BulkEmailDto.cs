using System.ComponentModel.DataAnnotations;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Email;

public class BulkEmailDto
{
    [MaxLength(EmailRecipientLimits.MaxRecipients, ErrorMessage = "Send to at most 2000 email addresses at a time.")]
    public required List<string?> Emails { get; set; }
    public EmailLanguage Language { get; set; }
    public string? CustomMessage { get; set; }
    public string? CustomMessageFr { get; set; }
}