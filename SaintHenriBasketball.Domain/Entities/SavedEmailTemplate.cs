namespace SaintHenriBasketball.Domain.Entities;

public class SavedEmailTemplate
{
    public Guid Id { get; private set; }
    public string Name { get; set; } = string.Empty;
    public string SubjectEn { get; set; } = string.Empty;
    public string SubjectFr { get; set; } = string.Empty;
    public string BodyEn { get; set; } = string.Empty;
    public string BodyFr { get; set; } = string.Empty;
    public string? EmailType { get; set; }
    public DateTime CreatedOn { get; private set; }

    private SavedEmailTemplate() { }

    public SavedEmailTemplate(string name, string subjectEn, string subjectFr, string bodyEn, string bodyFr)
    {
        Id = Guid.NewGuid();
        Name = name;
        SubjectEn = subjectEn;
        SubjectFr = subjectFr;
        BodyEn = bodyEn;
        BodyFr = bodyFr;
        CreatedOn = DateTime.UtcNow;
    }
}
