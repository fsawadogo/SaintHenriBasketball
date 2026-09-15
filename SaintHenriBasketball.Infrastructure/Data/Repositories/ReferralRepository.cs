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

    public async Task<IReadOnlyList<ReferralRedemption>> GetRedemptionsAsync(int page = 1, int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        return await _context.ReferralRedemptions
            .AsNoTracking()
            .OrderByDescending(r => r.RedeemedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    // Not tracked: status changes are conditional ExecuteUpdates, and a tracked copy would go stale after one.
    public async Task<ReferralRedemption?> GetRedemptionByIdAsync(Guid id) =>
        await _context.ReferralRedemptions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);

    public async Task<ReferralRedemption?> GetRedemptionByRefereeAsync(Guid refereeUserId) =>
        await _context.ReferralRedemptions.AsNoTracking().FirstOrDefaultAsync(r => r.RefereeUserId == refereeUserId);

    public async Task<bool> HasReferredAsync(Guid referrerUserId, Guid refereeUserId) =>
        await _context.ReferralRedemptions.AnyAsync(r => r.ReferrerUserId == referrerUserId && r.RefereeUserId == refereeUserId);

    public async Task<ReferralRedeemOutcome> TryRedeemAsync(ReferralRedemption redemption, ApplicationUser? newUser = null)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var counted = await _context.ReferralCodes
            .Where(c => c.Id == redemption.ReferralCodeId && (c.MaxUses == null || c.TimesUsed < c.MaxUses))
            .ExecuteUpdateAsync(update => update.SetProperty(c => c.TimesUsed, c => c.TimesUsed + 1));
        if (counted != 1) return ReferralRedeemOutcome.CodeUnavailable;

        if (newUser is not null) _context.Users.Add(newUser);
        _context.ReferralRedemptions.Add(redemption);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            DbUpdateExceptions.Detach(_context, redemption, newUser);
            // For a new account a duplicate is its email/username racing another sign-up, not a redemption.
            if (newUser is null && DbUpdateExceptions.IsUniqueViolation(ex)) return ReferralRedeemOutcome.AlreadyRedeemed;
            throw;
        }

        await transaction.CommitAsync();
        return ReferralRedeemOutcome.Redeemed;
    }

    public async Task<ReferralRedemption?> TryGrantRewardAsync(Guid redemptionId, decimal rewardAmount)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        var granted = await _context.ReferralRedemptions
            .Where(r => r.Id == redemptionId && r.RewardStatus == ReferralRewardStatus.Pending)
            .ExecuteUpdateAsync(update => update
                .SetProperty(r => r.RewardStatus, ReferralRewardStatus.Granted)
                .SetProperty(r => r.StatusChangedOn, (DateTime?)now));
        if (granted != 1) return null;

        var redemption = await _context.ReferralRedemptions.AsNoTracking().SingleAsync(r => r.Id == redemptionId);
        var reward = new AccountCredit(redemption.ReferrerUserId, rewardAmount, AccountCreditKind.ReferralReward, referralRedemptionId: redemptionId);
        _context.AccountCredits.Add(reward);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (DbUpdateExceptions.IsUniqueViolation(ex))
        {
            DbUpdateExceptions.Detach(_context, reward);
            return null;
        }

        await transaction.CommitAsync();
        return redemption;
    }

    public async Task<bool> TryRevokeAsync(Guid redemptionId) =>
        await _context.ReferralRedemptions
            .Where(r => r.Id == redemptionId && r.RewardStatus == ReferralRewardStatus.Pending)
            .ExecuteUpdateAsync(update => update
                .SetProperty(r => r.RewardStatus, ReferralRewardStatus.Revoked)
                .SetProperty(r => r.StatusChangedOn, (DateTime?)DateTime.UtcNow)) == 1;
}
