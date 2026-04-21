namespace SaintHenriBasketball.Application.DTOs.Waivers;

public class WaiverTemplateDto
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public string BodyEn { get; set; } = string.Empty;
    public string BodyFr { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class CurrentWaiverDto
{
    public WaiverTemplateDto? Template { get; set; }
    public bool UserHasAccepted { get; set; }
}

public class CreateWaiverTemplateDto
{
    public string BodyEn { get; set; } = string.Empty;
    public string BodyFr { get; set; } = string.Empty;
    public DateTime? EffectiveDate { get; set; }
    public bool Activate { get; set; } = true;
}
