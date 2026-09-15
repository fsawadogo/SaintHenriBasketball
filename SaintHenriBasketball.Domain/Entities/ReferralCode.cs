namespace SaintHenriBasketball.Domain.Entities;

public class ReferralCode
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public Guid OwnerUserId { get; private set; }
    /// Uses counted atomically with each redemption; the redeem flow compares it with <see cref="MaxUses"/>.
    public int TimesUsed { get; set; }
    /// Null means unlimited.
    public int? MaxUses { get; set; }
    // Nullable backing field: EF writes an explicit false, and an unset value takes the column default (true).
    private bool? _isActive;
    /// An admin can switch a code off; inactive codes cannot be redeemed.
    public bool IsActive { get => _isActive ?? true; set => _isActive = value; }
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
