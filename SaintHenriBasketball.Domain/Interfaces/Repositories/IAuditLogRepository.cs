using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLog>> GetAllAsync(int page = 1, int pageSize = 50, string? entityType = null);
    Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, Guid entityId);
    Task AddAsync(AuditLog log);
}
