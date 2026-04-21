namespace SaintHenriBasketball.Domain.Entities;

public class WaiverTemplate
{
    public Guid Id { get; private set; }
    public int Version { get; private set; }
    public string BodyEn { get; set; } = string.Empty;
    public string BodyFr { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOn { get; private set; }

    private WaiverTemplate() { }

    public WaiverTemplate(int version, string bodyEn, string bodyFr, DateTime effectiveDate, bool isActive = false)
    {
        Id = Guid.NewGuid();
        Version = version;
        BodyEn = bodyEn;
        BodyFr = bodyFr;
        EffectiveDate = effectiveDate;
        IsActive = isActive;
        CreatedOn = DateTime.UtcNow;
    }
}
