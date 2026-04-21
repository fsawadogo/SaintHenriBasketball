using Microsoft.EntityFrameworkCore;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Interfaces.Repositories;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Data.Repositories;

public class ReferralRepository : IReferralRepository
{
    private readonly ApplicationDbContext _context;

    public ReferralRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReferralCode?> GetCodeByOwnerAsync(Guid ownerUserId) =>
        await _context.ReferralCodes.FirstOrDefaultAsync(c => c.OwnerUserId == ownerUserId);

    public async Task<ReferralCode?> GetCodeByValueAsync(string code) =>
        await _context.ReferralCodes.FirstOrDefaultAsync(c => c.Code == code);

    public async Task AddCodeAsync(ReferralCode code)
    {
        await _context.ReferralCodes.AddAsync(code);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateCodeAsync(ReferralCode code)
    {
        _context.ReferralCodes.Update(code);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasRefereeRedeemedAsync(Guid refereeUserId) =>
        await _context.ReferralRedemptions.AnyAsync(r => r.RefereeUserId == refereeUserId);

    public async Task AddRedemptionAsync(ReferralRedemption redemption)
    {
        await _context.ReferralRedemptions.AddAsync(redemption);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateRedemptionAsync(ReferralRedemption redemption)
    {
        redemption.StatusChangedOn = DateTime.UtcNow;
        _context.ReferralRedemptions.Update(redemption);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ReferralRedemption>> GetRedemptionsAsync(int page = 1, int pageSize = 50) =>
        await _context.ReferralRedemptions
            .AsNoTracking()
            .OrderByDescending(r => r.RedeemedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<ReferralRedemption?> GetRedemptionByIdAsync(Guid id) =>
        await _context.ReferralRedemptions.FirstOrDefaultAsync(r => r.Id == id);
}
