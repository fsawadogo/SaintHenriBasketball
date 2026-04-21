using SaintHenriBasketball.Application.DTOs.SessionTemplate;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface ISessionTemplateService
{
    Task<IReadOnlyList<SessionTemplateDto>> GetAllAsync();
    Task<SessionTemplateDto> CreateAsync(UpsertSessionTemplateDto body);
    Task<SessionTemplateDto> UpdateAsync(Guid id, UpsertSessionTemplateDto body);
    Task DeleteAsync(Guid id);
    Task<GenerateSessionsResultDto> GenerateSessionsAsync(Guid templateId, DateTime startDate, DateTime endDate);
}
