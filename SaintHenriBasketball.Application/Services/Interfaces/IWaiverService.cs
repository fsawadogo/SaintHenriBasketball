using SaintHenriBasketball.Application.DTOs.Waivers;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IWaiverService
{
    Task<CurrentWaiverDto> GetCurrentAsync(Guid userId);
    Task AcceptCurrentAsync(Guid userId, string? ipAddress);
    Task<IReadOnlyList<WaiverTemplateDto>> GetAllTemplatesAsync();
    Task<WaiverTemplateDto> CreateTemplateAsync(CreateWaiverTemplateDto body);
}
