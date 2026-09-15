namespace SaintHenriBasketball.Domain.Interfaces.Repositories;

/// Filters for the audit log. From and To are UTC instants; Action matches any part of the action name.
public record AuditLogSearchCriteria(
    Guid? UserId = null,
    string? EntityType = null,
    string? Action = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 50);

/// Someone who has entries in the audit log, under the name they last had.
public record AuditLogActor(Guid UserId, string UserName);
