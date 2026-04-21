namespace SaintHenriBasketball.Application.DTOs.Referrals;

public class ReferralCodeDto
{
    public string Code { get; set; } = string.Empty;
    public int TimesUsed { get; set; }
    public int? MaxUses { get; set; }
    public string ShareUrl { get; set; } = string.Empty;
}

public class RedeemReferralCodeDto
{
    public string Code { get; set; } = string.Empty;
}

public class ReferralRedemptionDto
{
    public Guid Id { get; set; }
    public Guid ReferrerUserId { get; set; }
    public string ReferrerName { get; set; } = string.Empty;
    public Guid RefereeUserId { get; set; }
    public string RefereeName { get; set; } = string.Empty;
    public int RewardStatus { get; set; }
    public DateTime RedeemedOn { get; set; }
}

public class UpdateRedemptionStatusDto
{
    public int RewardStatus { get; set; }
}
