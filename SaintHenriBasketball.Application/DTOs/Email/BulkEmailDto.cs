using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Email;

public class BulkEmailDto
{
    public required List<string?> Emails { get; set; }
    public EmailLanguage Language { get; set; }
    public string? CustomMessage { get; set; }
    public string? CustomMessageFr { get; set; }
}