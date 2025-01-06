using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SeasonSubscriptionRepository : ISeasonSubscriptionRepository
{
    private readonly ApplicationDbContext _context;

    public SeasonSubscriptionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SeasonSubscription> GetByIdAsync(Guid id)
    {
        return await _context.SeasonSubscriptions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<SeasonSubscription> GetActiveSubscriptionByUserIdAsync(Guid userId)
    {
        return await _context.SeasonSubscriptions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s =>
                s.UserId == userId &&
                s.IsActive &&
                s.EndDate > DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<SeasonSubscription>> GetAllAsync()
    {
        return await _context.SeasonSubscriptions
            .Include(s => s.User)
            .OrderByDescending(s => s.CreatedOn)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<SeasonSubscription>> GetActiveSubscriptionsAsync()
    {
        return await _context.SeasonSubscriptions
            .Include(s => s.User)
            .Where(s => s.IsActive && s.EndDate > DateTime.UtcNow)
            .OrderBy(s => s.EndDate)
            .ToListAsync();
    }

    public async Task AddAsync(SeasonSubscription subscription)
    {
        await _context.SeasonSubscriptions.AddAsync(subscription);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SeasonSubscription subscription)
    {
        _context.SeasonSubscriptions.Update(subscription);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasActiveSubscriptionAsync(Guid userId)
    {
        return await _context.SeasonSubscriptions
            .AnyAsync(s =>
                s.UserId == userId &&
                s.IsActive &&
                s.EndDate > DateTime.UtcNow);
    }
}
