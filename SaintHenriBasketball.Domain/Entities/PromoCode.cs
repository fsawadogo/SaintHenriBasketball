namespace SaintHenriBasketball.Domain.Entities;

public enum PromoDiscountType
{
    Percent = 0,
    Fixed = 1,
}

public enum PromoAppliesTo
{
    DropIn = 0,
    Season = 1,
    Both = 2,
}

public class PromoCode
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public PromoDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }
    public int? MaxUses { get; set; }
    public int TimesUsed { get; set; }
    public PromoAppliesTo AppliesTo { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOn { get; private set; }

    private PromoCode() { }

    public PromoCode(string code, PromoDiscountType discountType, decimal discountValue,
        DateTime validFrom, DateTime validUntil, PromoAppliesTo appliesTo,
        int? maxUses = null, bool isActive = true)
    {
        Id = Guid.NewGuid();
        Code = code ?? throw new ArgumentNullException(nameof(code));
        DiscountType = discountType;
        DiscountValue = discountValue;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        AppliesTo = appliesTo;
        MaxUses = maxUses;
        TimesUsed = 0;
        IsActive = isActive;
        CreatedOn = DateTime.UtcNow;
    }
}
