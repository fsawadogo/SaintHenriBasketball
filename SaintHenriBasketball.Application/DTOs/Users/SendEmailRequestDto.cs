using SaintHenriBasketball.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SaintHenriBasketball.Application.DTOs.Users;

public class SendEmailRequestDto
{
    [Required]
    public EmailType EmailType { get; set; }

    [Required]
    public List<string> Emails { get; set; } = new();

    public EmailLanguage Language { get; set; } = EmailLanguage.English;

    public string? CustomMessage { get; set; }

    public string? CustomMessageFr { get; set; }

    /// <summary>
    /// Validates that all email addresses are in correct format
    /// </summary>
    public bool HasValidEmails()
    {
        return Emails != null &&
               Emails.Any() &&
               Emails.All(email => new EmailAddressAttribute().IsValid(email));
    }
}