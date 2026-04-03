using SaintHenriBasketball.Application.DTOs.AuditLog;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _repository;

    public AuditLogService(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task LogAsync(string action, string entityType, Guid? entityId = null,
        string? details = null, Guid? userId = null, string userName = "System")
    {
        var log = new AuditLog(action, entityType, entityId, details, userId, userName);
        await _repository.AddAsync(log);
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetLogsAsync(int page = 1, int pageSize = 50, string? entityType = null)
    {
        var logs = await _repository.GetAllAsync(page, pageSize, entityType);
        return logs.Select(l => new AuditLogDto
        {
            Id = l.Id,
            UserId = l.UserId,
            UserName = l.UserName,
            Action = l.Action,
            EntityType = l.EntityType,
            EntityId = l.EntityId,
            Details = l.Details,
            CreatedAt = l.CreatedAt,
        }).ToList();
    }
}
