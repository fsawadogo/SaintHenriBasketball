namespace SaintHenriBasketball.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; private set; }

    private AuditLog() { }

    public AuditLog(string action, string entityType, Guid? entityId = null, string? details = null, Guid? userId = null, string userName = "System")
    {
        Id = Guid.NewGuid();
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Details = details;
        UserId = userId;
        UserName = userName;
        CreatedAt = DateTime.UtcNow;
    }
}
