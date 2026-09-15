namespace SaintHenriBasketball.Application.DTOs.AuditLog;

public class AuditLogQuery
{
    public Guid? UserId { get; set; }
    public string? EntityType { get; set; }
    /// Matches any part of the action name.
    public string? Action { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class AuditLogPageDto
{
    public IReadOnlyList<AuditLogDto> Items { get; set; } = Array.Empty<AuditLogDto>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class AuditLogActorDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
}

/// Choices for the audit log filters, taken from what the log actually contains.
public class AuditLogFiltersDto
{
    public IReadOnlyList<string> EntityTypes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<AuditLogActorDto> Actors { get; set; } = Array.Empty<AuditLogActorDto>();
}
