using System.ComponentModel.DataAnnotations;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Email;

public class DropInPaymentLinkEmailDto
{
    [MaxLength(EmailRecipientLimits.MaxRecipients, ErrorMessage = "Send to at most 2000 email addresses at a time.")]
    public List<string> Emails { get; set; } = new();
    public EmailLanguage Language { get; set; }
    public Guid SessionId { get; set; }
    public decimal Amount { get; set; }
}
