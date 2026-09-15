using SaintHenriBasketball.Application.DTOs.AuditLog;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;

namespace SaintHenriBasketball.Application.Services.Implementations;

public class AuditLogService : IAuditLogService
{
    public const string DateRangeMessage = "The start date must be on or before the end date.";
    public const int MaxPageSize = 200;

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
        return logs.Select(ToDto).ToList();
    }

    public async Task<AuditLogPageDto> SearchAsync(AuditLogQuery query)
    {
        if (query.From is DateTime from && query.To is DateTime to && from > to)
            throw new ValidationException(DateRangeMessage);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var (items, total) = await _repository.SearchAsync(new AuditLogSearchCriteria(
            query.UserId,
            string.IsNullOrWhiteSpace(query.EntityType) ? null : query.EntityType.Trim(),
            string.IsNullOrWhiteSpace(query.Action) ? null : query.Action.Trim(),
            query.From,
            query.To,
            page,
            pageSize));
        return new AuditLogPageDto { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<AuditLogFiltersDto> GetFiltersAsync() => new()
    {
        EntityTypes = await _repository.GetEntityTypesAsync(),
        Actors = (await _repository.GetActorsAsync())
            .Select(a => new AuditLogActorDto { UserId = a.UserId, UserName = a.UserName })
            .ToList(),
    };

    private static AuditLogDto ToDto(AuditLog l) => new()
    {
        Id = l.Id,
        UserId = l.UserId,
        UserName = l.UserName,
        Action = l.Action,
        EntityType = l.EntityType,
        EntityId = l.EntityId,
        Details = l.Details,
        // Stored in UTC; SQL Server returns it without a kind, so mark it before it serializes.
        CreatedAt = DateTime.SpecifyKind(l.CreatedAt, DateTimeKind.Utc),
    };
}
