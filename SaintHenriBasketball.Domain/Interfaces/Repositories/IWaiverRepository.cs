using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IWaiverRepository
{
    Task<IReadOnlyList<WaiverTemplate>> GetAllTemplatesAsync();
    Task<WaiverTemplate?> GetActiveTemplateAsync();
    Task<WaiverTemplate?> GetTemplateByIdAsync(Guid id);
    Task AddTemplateAsync(WaiverTemplate template);
    Task UpdateTemplateAsync(WaiverTemplate template);

    Task<WaiverAcceptance?> GetAcceptanceAsync(Guid userId, int version);
    Task AddAcceptanceAsync(WaiverAcceptance acceptance);
}
