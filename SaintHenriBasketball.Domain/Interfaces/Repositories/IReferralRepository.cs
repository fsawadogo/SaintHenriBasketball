using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

public enum ReferralRedeemOutcome
{
    Redeemed,
    /// The code reached MaxUses (or disappeared) before this use was counted.
    CodeUnavailable,
    /// The referee already has a redemption (unique RefereeUserId).
    AlreadyRedeemed,
    /// An admin deactivated the code before this use was counted.
    CodeInactive,
}

public enum ReferralCodeUpdateOutcome
{
    Updated,
    NotFound,
    /// MaxUses would be below the code's current uses and the caller did not allow it.
    BelowCurrentUses,
}

/// Admin list filters. Search matches the code, the owner's name or email. Page is 1-based.
public record ReferralCodeSearchCriteria(string? Search = null, int Page = 1, int PageSize = 50);

public record ReferralCodeAdminRow(
    ReferralCode Code,
    string? OwnerFirstName,
    string? OwnerLastName,
    string? OwnerEmail,
    int PendingRedemptions,
    int GrantedRedemptions,
    int RevokedRedemptions,
    int RewardsGrantedCount,
    decimal RewardsGrantedTotal);

public record ReferralCodeAdminPage(IReadOnlyList<ReferralCodeAdminRow> Items, int Total);

public interface IReferralRepository
{
    Task<ReferralCode?> GetCodeByOwnerAsync(Guid ownerUserId);
    Task<ReferralCode?> GetCodeByValueAsync(string code);
    Task AddCodeAsync(ReferralCode code);
    Task UpdateCodeAsync(ReferralCode code);

    Task<bool> HasRefereeRedeemedAsync(Guid refereeUserId);
    Task AddRedemptionAsync(ReferralRedemption redemption);
    Task UpdateRedemptionAsync(ReferralRedemption redemption);
    Task<IReadOnlyList<ReferralRedemption>> GetRedemptionsAsync(int page = 1, int pageSize = 50);
    Task<ReferralRedemption?> GetRedemptionByIdAsync(Guid id);
    Task<ReferralRedemption?> GetRedemptionByRefereeAsync(Guid refereeUserId);

    /// True when <paramref name="referrerUserId"/> referred <paramref name="refereeUserId"/>.
    Task<bool> HasReferredAsync(Guid referrerUserId, Guid refereeUserId);

    /// <summary>
    /// Atomically counts one use of the code (only while under MaxUses) and records the redemption.
    /// When <paramref name="newUser"/> is given it is inserted in the same transaction, so a
    /// rejected code creates no account.
    /// </summary>
    Task<ReferralRedeemOutcome> TryRedeemAsync(ReferralRedemption redemption, ApplicationUser? newUser = null);

    /// <summary>
    /// In one transaction: moves the redemption Pending → Granted and inserts the referrer's
    /// ReferralReward credit. Returns the granted redemption, or null when it was not Pending
    /// (or the credit already exists), in which case nothing is written.
    /// </summary>
    Task<ReferralRedemption?> TryGrantRewardAsync(Guid redemptionId, decimal rewardAmount);

    /// Moves the redemption Pending → Revoked. False when it was not Pending.
    Task<bool> TryRevokeAsync(Guid redemptionId);

    /// Untracked read of one code.
    Task<ReferralCode?> GetCodeByIdAsync(Guid id);

    /// Codes with their owner, redemption counts by status and referral rewards granted, newest first.
    Task<ReferralCodeAdminPage> SearchCodesAsync(ReferralCodeSearchCriteria criteria);

    Task<ReferralCodeAdminRow?> GetCodeAdminRowAsync(Guid id);

    /// <summary>
    /// Sets IsActive and MaxUses in one conditional update. Unless <paramref name="allowBelowCurrentUses"/>,
    /// the update only applies while TimesUsed is at most the new MaxUses, so a redemption racing the
    /// change cannot leave the limit below the uses already counted.
    /// </summary>
    Task<ReferralCodeUpdateOutcome> TryUpdateCodeLimitsAsync(Guid id, bool isActive, int? maxUses, bool allowBelowCurrentUses);
}
