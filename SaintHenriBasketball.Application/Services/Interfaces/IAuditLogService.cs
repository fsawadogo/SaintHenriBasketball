using SaintHenriBasketball.Application.DTOs.AuditLog;

namespace SaintHenriBasketball.Application.Services.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(string action, string entityType, Guid? entityId = null, string? details = null, Guid? userId = null, string userName = "System");
    Task<IReadOnlyList<AuditLogDto>> GetLogsAsync(int page = 1, int pageSize = 50, string? entityType = null);

    /// Filtered page of entries with the total match count. Throws ValidationException for a reversed date range.
    Task<AuditLogPageDto> SearchAsync(AuditLogQuery query);

    /// The entity types and admins that appear in the log, for the filter choices.
    Task<AuditLogFiltersDto> GetFiltersAsync();
}
