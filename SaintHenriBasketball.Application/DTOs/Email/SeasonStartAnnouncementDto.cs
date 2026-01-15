using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Application.DTOs.Email;

public class SeasonStartAnnouncementDto
{
    public EmailLanguage Language { get; set; } = EmailLanguage.English;
    
    public string? CustomMessage { get; set; }
    
    public string? CustomMessageFr { get; set; }
}
