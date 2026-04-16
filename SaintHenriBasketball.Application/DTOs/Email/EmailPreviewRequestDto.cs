using System.ComponentModel.DataAnnotations;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Email;

public class EmailPreviewRequestDto
{
    [Required]
    public EmailType EmailType { get; set; }

    public EmailLanguage Language { get; set; } = EmailLanguage.French;

    public string? CustomMessage { get; set; }

    public string? CustomMessageFr { get; set; }
}
