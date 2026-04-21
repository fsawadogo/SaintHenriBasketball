using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class SessionTemplateRepository : ISessionTemplateRepository
{
    private readonly ApplicationDbContext _context;

    public SessionTemplateRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SessionTemplate>> GetAllAsync() =>
        await _context.SessionTemplates.OrderBy(t => t.DayOfWeek).ToListAsync();

    public async Task<SessionTemplate?> GetByIdAsync(Guid id) =>
        await _context.SessionTemplates.FirstOrDefaultAsync(t => t.Id == id);

    public async Task AddAsync(SessionTemplate template)
    {
        await _context.SessionTemplates.AddAsync(template);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SessionTemplate template)
    {
        _context.SessionTemplates.Update(template);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _context.SessionTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (entity is null) return;
        _context.SessionTemplates.Remove(entity);
        await _context.SaveChangesAsync();
    }
}
