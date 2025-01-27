using System.ComponentModel.DataAnnotations;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Email;

public class CustomEmailRequestDto
{
    [Required]
    public EmailType EmailType { get; set; }

    [Required]
    public List<string> Emails { get; set; } = new();

    [Required]
    public EmailLanguage Language { get; set; } = EmailLanguage.English;

    public string? CustomMessage { get; set; }

    public string? CustomMessageFr { get; set; }
}