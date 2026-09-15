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
        // Same pattern as the promo reservation: the use is counted only if the row still qualifies,
        // so concurrent redemptions cannot pass MaxUses or slip in after a deactivation.
        var counted = await _context.ReferralCodes
            .Where(c => c.Id == redemption.ReferralCodeId && c.IsActive && (c.MaxUses == null || c.TimesUsed < c.MaxUses))
            .ExecuteUpdateAsync(update => update.SetProperty(c => c.TimesUsed, c => c.TimesUsed + 1));
        if (counted != 1)
        {
            var isActive = await _context.ReferralCodes.Where(c => c.Id == redemption.ReferralCodeId)
                .Select(c => (bool?)c.IsActive).FirstOrDefaultAsync();
            return isActive == false ? ReferralRedeemOutcome.CodeInactive : ReferralRedeemOutcome.CodeUnavailable;
        }

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

    public async Task<ReferralCode?> GetCodeByIdAsync(Guid id) =>
        await _context.ReferralCodes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

    public async Task<ReferralCodeAdminPage> SearchCodesAsync(ReferralCodeSearchCriteria criteria)
    {
        var query = AdminRows();
        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var term = criteria.Search.Trim();
            query = query.Where(r => r.Code.Code.Contains(term)
                || (r.OwnerEmail != null && r.OwnerEmail.Contains(term))
                || ((r.OwnerFirstName ?? "") + " " + (r.OwnerLastName ?? "")).Contains(term));
        }

        var total = await query.CountAsync();
        var page = Math.Max(1, criteria.Page);
        var pageSize = Math.Max(1, criteria.PageSize);
        var rows = await WithCounts(query
                .OrderByDescending(r => r.Code.CreatedOn)
                .ThenBy(r => r.Code.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync();
        return new ReferralCodeAdminPage(rows.Select(ToRow).ToList(), total);
    }

    public async Task<ReferralCodeAdminRow?> GetCodeAdminRowAsync(Guid id)
    {
        var row = await WithCounts(AdminRows().Where(r => r.Code.Id == id)).FirstOrDefaultAsync();
        return row is null ? null : ToRow(row);
    }

    public async Task<ReferralCodeUpdateOutcome> TryUpdateCodeLimitsAsync(Guid id, bool isActive, int? maxUses, bool allowBelowCurrentUses)
    {
        var updated = await _context.ReferralCodes
            .Where(c => c.Id == id && (allowBelowCurrentUses || maxUses == null || c.TimesUsed <= maxUses))
            .ExecuteUpdateAsync(update => update
                .SetProperty(c => c.IsActive, isActive)
                .SetProperty(c => c.MaxUses, maxUses));
        if (updated == 1) return ReferralCodeUpdateOutcome.Updated;
        return await _context.ReferralCodes.AnyAsync(c => c.Id == id)
            ? ReferralCodeUpdateOutcome.BelowCurrentUses
            : ReferralCodeUpdateOutcome.NotFound;
    }

    private sealed class CodeWithOwner
    {
        public ReferralCode Code { get; init; } = null!;
        public string? OwnerFirstName { get; init; }
        public string? OwnerLastName { get; init; }
        public string? OwnerEmail { get; init; }
    }

    private sealed class CodeWithCounts
    {
        public CodeWithOwner Row { get; init; } = null!;
        public int Pending { get; init; }
        public int Granted { get; init; }
        public int Revoked { get; init; }
        public int RewardsCount { get; init; }
        public decimal? RewardsTotal { get; init; }
    }

    // Left join: codes have no foreign key to their owner, so a missing account still lists the code.
    private IQueryable<CodeWithOwner> AdminRows() =>
        from c in _context.ReferralCodes.AsNoTracking()
        join u in _context.Users on c.OwnerUserId equals u.Id into owners
        from u in owners.DefaultIfEmpty()
        select new CodeWithOwner { Code = c, OwnerFirstName = u.FirstName, OwnerLastName = u.LastName, OwnerEmail = u.Email };

    private IQueryable<CodeWithCounts> WithCounts(IQueryable<CodeWithOwner> rows) =>
        rows.Select(r => new CodeWithCounts
        {
            Row = r,
            Pending = _context.ReferralRedemptions.Count(x => x.ReferralCodeId == r.Code.Id && x.RewardStatus == ReferralRewardStatus.Pending),
            Granted = _context.ReferralRedemptions.Count(x => x.ReferralCodeId == r.Code.Id && x.RewardStatus == ReferralRewardStatus.Granted),
            Revoked = _context.ReferralRedemptions.Count(x => x.ReferralCodeId == r.Code.Id && x.RewardStatus == ReferralRewardStatus.Revoked),
            RewardsCount = _context.AccountCredits.Count(a => a.Kind == AccountCreditKind.ReferralReward
                && _context.ReferralRedemptions.Any(x => x.Id == a.ReferralRedemptionId && x.ReferralCodeId == r.Code.Id)),
            RewardsTotal = _context.AccountCredits.Where(a => a.Kind == AccountCreditKind.ReferralReward
                && _context.ReferralRedemptions.Any(x => x.Id == a.ReferralRedemptionId && x.ReferralCodeId == r.Code.Id))
                .Sum(a => (decimal?)a.Amount),
        });

    private static ReferralCodeAdminRow ToRow(CodeWithCounts r) => new(
        r.Row.Code, r.Row.OwnerFirstName, r.Row.OwnerLastName, r.Row.OwnerEmail,
        r.Pending, r.Granted, r.Revoked, r.RewardsCount, r.RewardsTotal ?? 0m);
}
