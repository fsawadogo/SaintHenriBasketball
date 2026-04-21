namespace SaintHenriBasketball.Domain.Entities;

public class FeatureFlag
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Description { get; set; } = string.Empty;
    public string DescriptionFr { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedOn { get; private set; }
    public DateTime? ModifiedOn { get; set; }

    private FeatureFlag() { }

    public FeatureFlag(string key, string description, string descriptionFr, bool isPublic = true, bool enabled = false)
    {
        Id = Guid.NewGuid();
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Description = description ?? string.Empty;
        DescriptionFr = descriptionFr ?? string.Empty;
        IsPublic = isPublic;
        Enabled = enabled;
        CreatedOn = DateTime.UtcNow;
    }
}
