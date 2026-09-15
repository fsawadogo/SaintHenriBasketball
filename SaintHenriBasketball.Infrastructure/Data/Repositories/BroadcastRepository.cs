using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class BroadcastRepository(ApplicationDbContext context) : IBroadcastRepository
{
    public async Task AddAsync(BroadcastMessage message)
    {
        context.BroadcastMessages.Add(message);
        await context.SaveChangesAsync();
    }

    public Task<BroadcastMessage?> GetByIdAsync(Guid id) =>
        context.BroadcastMessages.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);

    public async Task<(IReadOnlyList<BroadcastMessage> Items, int Total)> GetPageAsync(int page, int pageSize)
    {
        var query = context.BroadcastMessages.AsNoTracking();
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.QueuedAt)
            .ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    public async Task RecordDeliveryAsync(Guid id, BroadcastStatus status, int attempted, int succeeded, int failed)
    {
        DateTime? completedAt = DateTime.UtcNow;
        await context.BroadcastMessages
            .Where(b => b.Id == id)
            .ExecuteUpdateAsync(update => update
                .SetProperty(b => b.Status, status)
                .SetProperty(b => b.Attempted, attempted)
                .SetProperty(b => b.Succeeded, succeeded)
                .SetProperty(b => b.Failed, failed)
                .SetProperty(b => b.CompletedAt, completedAt));
    }

    public async Task MarkFailedAsync(Guid id)
    {
        DateTime? completedAt = DateTime.UtcNow;
        await context.BroadcastMessages
            .Where(b => b.Id == id && b.Status == BroadcastStatus.Queued)
            .ExecuteUpdateAsync(update => update
                .SetProperty(b => b.Status, BroadcastStatus.Failed)
                .SetProperty(b => b.CompletedAt, completedAt));
    }
}
