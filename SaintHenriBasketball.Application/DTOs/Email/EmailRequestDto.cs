using SaintHenriBasketball.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SaintHenriBasketball.Application.DTOs.Email;

public class EmailRequestDto
{
    [Required]
    public List<string?> Emails { get; set; } = new();

    [Required]
    public EmailLanguage Language { get; set; } = EmailLanguage.English;

    public string? CustomMessage { get; set; }

    public string? CustomMessageFr { get; set; }

    public bool HasValidEmails()
    {
        return Emails != null &&
               Emails.Any() &&
               Emails.All(email => new EmailAddressAttribute().IsValid(email));
    }
}
