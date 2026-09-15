using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Application.DTOs.PromoReferralReports;

/// Optional inclusive UTC range. Promo usage filters on the payment date; credits on the ledger entry date.
public class ReportRangeQuery
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class PromoUsageReportDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public DateTime GeneratedAt { get; set; }
    /// How TimesUsedCounter differs from TimesUsed.
    public string UsageNote { get; set; } = string.Empty;
    public IReadOnlyList<PromoUsageRowDto> Items { get; set; } = Array.Empty<PromoUsageRowDto>();
    public PromoUsageTotalsDto Totals { get; set; } = new();
}

public class PromoUsageRowDto
{
    public Guid PromoCodeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public PromoDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public PromoAppliesTo AppliesTo { get; set; }
    public bool IsActive { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }
    public bool IsExpired { get; set; }
    public int? MaxUses { get; set; }
    /// Completed payments with this promo in the range. Refunded payments are not uses.
    public int TimesUsed { get; set; }
    /// Refunded payments with this promo in the range.
    public int RefundedUses { get; set; }
    /// The promo code's own reservation counter (all time, never decremented).
    public int TimesUsedCounter { get; set; }
    public decimal TotalListPrice { get; set; }
    public decimal TotalDiscount { get; set; }
    /// List price minus discount of the completed uses (account credit is reported separately).
    public decimal RevenueAfterDiscount { get; set; }
    public DateTime? FirstUsedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class PromoUsageTotalsDto
{
    public int PromoCodes { get; set; }
    public int CodesUsed { get; set; }
    public int TimesUsed { get; set; }
    public int RefundedUses { get; set; }
    public decimal TotalListPrice { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal RevenueAfterDiscount { get; set; }
}

public class ReferralCodeAdminQuery
{
    /// Matches the code, the owner's name or email.
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class ReferralCodeAdminPageDto
{
    public IReadOnlyList<ReferralCodeAdminDto> Items { get; set; } = Array.Empty<ReferralCodeAdminDto>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ReferralCodeAdminDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerEmail { get; set; }
    public bool IsActive { get; set; }
    /// Null means unlimited.
    public int? MaxUses { get; set; }
    /// The counter the redeem flow compares with MaxUses.
    public int TimesUsed { get; set; }
    public ReferralRedemptionCountsDto Redemptions { get; set; } = new();
    public int RewardsGrantedCount { get; set; }
    public decimal RewardsGrantedTotal { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class ReferralRedemptionCountsDto
{
    public int Pending { get; set; }
    public int Granted { get; set; }
    public int Revoked { get; set; }
    public int Total { get; set; }
}

public class UpdateReferralCodeDto
{
    public bool IsActive { get; set; } = true;
    /// At least 1, or null for unlimited.
    public int? MaxUses { get; set; }
    /// Allows MaxUses below the uses already counted; new redemptions are then simply blocked.
    public bool AllowBelowCurrentUses { get; set; }
}

public class CreditsReportDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public DateTime GeneratedAt { get; set; }
    /// Positive ledger entries by kind (referral rewards, released, refunds, manual additions).
    public IReadOnlyList<CreditKindTotalDto> Granted { get; set; } = Array.Empty<CreditKindTotalDto>();
    public decimal GrantedTotal { get; set; }
    /// Credit applied to payments. Total is a positive amount.
    public CreditAmountDto Spent { get; set; } = new();
    public CreditAmountDto RefundsAsCredit { get; set; } = new();
    public ManualAdjustmentsDto ManualAdjustments { get; set; } = new();
    public OutstandingCreditDto Outstanding { get; set; } = new();
}

public class CreditKindTotalDto
{
    public AccountCreditKind Kind { get; set; }
    public string KindName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Total { get; set; }
}

public class CreditAmountDto
{
    public int Count { get; set; }
    public decimal Total { get; set; }
}

public class ManualAdjustmentsDto
{
    public CreditAmountDto Added { get; set; } = new();
    /// Total is a positive amount.
    public CreditAmountDto Removed { get; set; } = new();
    public decimal Net { get; set; }
}

public class OutstandingCreditDto
{
    /// The end of the range, or the time of the report when there is none.
    public DateTime AsOf { get; set; }
    public decimal Balance { get; set; }
    public int PlayersWithBalance { get; set; }
}
