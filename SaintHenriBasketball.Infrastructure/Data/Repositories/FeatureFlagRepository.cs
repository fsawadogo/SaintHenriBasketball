using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class FeatureFlagRepository : IFeatureFlagRepository
{
    private readonly ApplicationDbContext _context;

    public FeatureFlagRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<FeatureFlag>> GetAllAsync() =>
        await _context.FeatureFlags.AsNoTracking().OrderBy(f => f.Key).ToListAsync();

    public async Task<FeatureFlag?> GetByKeyAsync(string key) =>
        await _context.FeatureFlags.FirstOrDefaultAsync(f => f.Key == key);

    public async Task AddAsync(FeatureFlag flag)
    {
        await _context.FeatureFlags.AddAsync(flag);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(FeatureFlag flag)
    {
        flag.ModifiedOn = DateTime.UtcNow;
        _context.FeatureFlags.Update(flag);
        await _context.SaveChangesAsync();
    }
}
