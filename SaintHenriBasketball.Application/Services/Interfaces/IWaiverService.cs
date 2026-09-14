using SaintHenriBasketball.Application.DTOs.Waivers;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IWaiverService
{
    Task<CurrentWaiverDto> GetCurrentAsync(Guid userId);
    Task AcceptCurrentAsync(Guid userId, string? ipAddress);
    /// Throws ValidationException when the waiver flag is on and the user hasn't accepted the active waiver.
    Task EnsureAcceptedAsync(Guid userId);
    Task<IReadOnlyList<WaiverTemplateDto>> GetAllTemplatesAsync();
    Task<WaiverTemplateDto> CreateTemplateAsync(CreateWaiverTemplateDto body);
    /// Who accepted a waiver version, and how many confirmed accounts still haven't.
    Task<WaiverAcceptancesDto> GetAcceptancesAsync(int version);
}
