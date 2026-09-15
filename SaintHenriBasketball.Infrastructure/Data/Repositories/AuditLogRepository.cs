using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly ApplicationDbContext _context;

    public AuditLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    private const int MaxPageSize = 200;

    public async Task<IReadOnlyList<AuditLog>> GetAllAsync(int page = 1, int pageSize = 50, string? entityType = null)
    {
        var (items, _) = await SearchAsync(new AuditLogSearchCriteria(EntityType: entityType, Page: page, PageSize: pageSize));
        return items;
    }

    public async Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, Guid entityId)
    {
        return await _context.AuditLogs
            .Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(AuditLog log)
    {
        await _context.AuditLogs.AddAsync(log);
        await _context.SaveChangesAsync();
    }

    public async Task<(IReadOnlyList<AuditLog> Items, int Total)> SearchAsync(AuditLogSearchCriteria criteria)
    {
        // Out-of-range paging used to reach SQL as a negative OFFSET (500).
        var page = Math.Max(1, criteria.Page);
        var pageSize = Math.Clamp(criteria.PageSize, 1, MaxPageSize);
        var query = _context.AuditLogs.AsNoTracking();
        if (criteria.UserId is Guid userId) query = query.Where(l => l.UserId == userId);
        if (!string.IsNullOrWhiteSpace(criteria.EntityType)) query = query.Where(l => l.EntityType == criteria.EntityType);
        if (!string.IsNullOrWhiteSpace(criteria.Action)) query = query.Where(l => l.Action.Contains(criteria.Action));
        if (criteria.From is DateTime from) query = query.Where(l => l.CreatedAt >= from);
        if (criteria.To is DateTime to) query = query.Where(l => l.CreatedAt <= to);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .ThenBy(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    public async Task<IReadOnlyList<string>> GetEntityTypesAsync() =>
        await _context.AuditLogs.Select(l => l.EntityType).Distinct().OrderBy(t => t).ToListAsync();

    public async Task<IReadOnlyList<AuditLogActor>> GetActorsAsync()
    {
        var latest = await _context.AuditLogs
            .Where(l => l.UserId != null)
            .GroupBy(l => l.UserId)
            .Select(g => new { UserId = g.Key!.Value, LastAt = g.Max(l => l.CreatedAt) })
            .ToListAsync();
        var names = await _context.AuditLogs
            .Where(l => l.UserId != null)
            .Select(l => new { UserId = l.UserId!.Value, l.UserName, l.CreatedAt })
            .ToListAsync();
        return latest
            .Select(a => new AuditLogActor(a.UserId, names.Where(n => n.UserId == a.UserId && n.CreatedAt == a.LastAt).Select(n => n.UserName).FirstOrDefault() ?? ""))
            .OrderBy(a => a.UserName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
