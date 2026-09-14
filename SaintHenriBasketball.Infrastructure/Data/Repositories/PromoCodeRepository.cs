using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class PromoCodeRepository : IPromoCodeRepository
{
    private readonly ApplicationDbContext _context;

    public PromoCodeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PromoCode>> GetAllAsync() =>
        await _context.PromoCodes.AsNoTracking().OrderByDescending(p => p.CreatedOn).ToListAsync();

    public async Task<PromoCode?> GetByIdAsync(Guid id) =>
        await _context.PromoCodes.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<PromoCode?> GetByCodeAsync(string code) =>
        await _context.PromoCodes.FirstOrDefaultAsync(p => p.Code == code);

    public async Task<bool> TryAddAsync(PromoCode promoCode)
    {
        await _context.PromoCodes.AddAsync(promoCode);
        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (DbUpdateExceptions.IsUniqueViolation(ex))
        {
            DbUpdateExceptions.Detach(_context, promoCode);
            return false;
        }
    }

    public async Task UpdateAsync(PromoCode promoCode)
    {
        _context.PromoCodes.Update(promoCode);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _context.PromoCodes.FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null) return;
        _context.PromoCodes.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsUsedByPaymentsAsync(Guid id) =>
        await _context.Payments.AnyAsync(p => p.PromoCodeId == id);
}
