using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Application.DTOs.PromoCodes;

public class PromoCodeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public PromoDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }
    public int? MaxUses { get; set; }
    public int TimesUsed { get; set; }
    public PromoAppliesTo AppliesTo { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class UpsertPromoCodeDto
{
    public string Code { get; set; } = string.Empty;
    public PromoDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }
    public int? MaxUses { get; set; }
    public PromoAppliesTo AppliesTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ValidatePromoCodeDto
{
    public string Code { get; set; } = string.Empty;
    public PromoAppliesTo TargetPlan { get; set; }
    public decimal Amount { get; set; }
}

public class ValidatePromoCodeResultDto
{
    public bool Valid { get; set; }
    public string? Reason { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
}
