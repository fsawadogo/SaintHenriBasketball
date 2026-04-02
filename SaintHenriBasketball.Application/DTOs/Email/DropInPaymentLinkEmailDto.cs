using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Email;

public class DropInPaymentLinkEmailDto
{
    public List<string> Emails { get; set; } = new();
    public EmailLanguage Language { get; set; }
    public Guid SessionId { get; set; }
    public decimal Amount { get; set; }
}
