namespace SaintHenriBasketball.Domain.Entities;

public enum ReferralRewardStatus
{
    Pending = 0,
    Granted = 1,
    Revoked = 2,
}

public class ReferralRedemption
{
    public Guid Id { get; private set; }
    public Guid ReferralCodeId { get; private set; }
    public Guid ReferrerUserId { get; private set; }
    public Guid RefereeUserId { get; private set; }
    public ReferralRewardStatus RewardStatus { get; set; }
    public DateTime RedeemedOn { get; private set; }
    public DateTime? StatusChangedOn { get; set; }

    private ReferralRedemption() { } // EF Core

    public ReferralRedemption(Guid referralCodeId, Guid referrerUserId, Guid refereeUserId)
    {
        Id = Guid.NewGuid();
        ReferralCodeId = referralCodeId;
        ReferrerUserId = referrerUserId;
        RefereeUserId = refereeUserId;
        RewardStatus = ReferralRewardStatus.Pending;
        RedeemedOn = DateTime.UtcNow;
    }
}
