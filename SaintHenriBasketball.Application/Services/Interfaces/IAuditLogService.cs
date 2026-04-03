using SaintHenriBasketball.Application.DTOs.AuditLog;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(string action, string entityType, Guid? entityId = null, string? details = null, Guid? userId = null, string userName = "System");
    Task<IReadOnlyList<AuditLogDto>> GetLogsAsync(int page = 1, int pageSize = 50, string? entityType = null);
}
