namespace SaintHenriBasketball.Application.DTOs.FeatureFlags;

public class FeatureFlagDto
{
    public string Key { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Description { get; set; } = string.Empty;
    public string DescriptionFr { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public DateTime CreatedOn { get; set; }
}
