namespace SaintHenriBasketball.Domain.Entities;

public class ReferralCode
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public Guid OwnerUserId { get; private set; }
    public int TimesUsed { get; set; }
    public int? MaxUses { get; set; }
    public DateTime CreatedOn { get; private set; }

    private ReferralCode() { } // EF Core

    public ReferralCode(string code, Guid ownerUserId, int? maxUses = null)
    {
        Id = Guid.NewGuid();
        Code = code ?? throw new ArgumentNullException(nameof(code));
        OwnerUserId = ownerUserId;
        MaxUses = maxUses;
        TimesUsed = 0;
        CreatedOn = DateTime.UtcNow;
    }
}
