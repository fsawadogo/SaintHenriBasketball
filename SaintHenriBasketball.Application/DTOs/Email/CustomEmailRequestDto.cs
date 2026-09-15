using System.ComponentModel.DataAnnotations;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Email;

public class CustomEmailRequestDto
{
    [Required]
    public EmailType EmailType { get; set; }

    [Required]
    [MaxLength(EmailRecipientLimits.MaxRecipients, ErrorMessage = "Send to at most 2000 email addresses at a time.")]
    public List<string> Emails { get; set; } = new();

    [Required]
    public EmailLanguage Language { get; set; } = EmailLanguage.English;

    public string? CustomMessage { get; set; }

    public string? CustomMessageFr { get; set; }
}