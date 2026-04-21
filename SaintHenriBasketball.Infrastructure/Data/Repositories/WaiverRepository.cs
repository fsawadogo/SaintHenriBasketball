using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class WaiverRepository : IWaiverRepository
{
    private readonly ApplicationDbContext _context;

    public WaiverRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WaiverTemplate>> GetAllTemplatesAsync() =>
        await _context.WaiverTemplates.OrderByDescending(t => t.Version).ToListAsync();

    public async Task<WaiverTemplate?> GetActiveTemplateAsync() =>
        await _context.WaiverTemplates
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync();

    public async Task<WaiverTemplate?> GetTemplateByIdAsync(Guid id) =>
        await _context.WaiverTemplates.FirstOrDefaultAsync(t => t.Id == id);

    public async Task AddTemplateAsync(WaiverTemplate template)
    {
        await _context.WaiverTemplates.AddAsync(template);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateTemplateAsync(WaiverTemplate template)
    {
        _context.WaiverTemplates.Update(template);
        await _context.SaveChangesAsync();
    }

    public async Task<WaiverAcceptance?> GetAcceptanceAsync(Guid userId, int version) =>
        await _context.WaiverAcceptances
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.WaiverVersion == version);

    public async Task AddAcceptanceAsync(WaiverAcceptance acceptance)
    {
        await _context.WaiverAcceptances.AddAsync(acceptance);
        await _context.SaveChangesAsync();
    }
}
