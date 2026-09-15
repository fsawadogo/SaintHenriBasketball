using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.AuditLog;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
[RequireFeature(FeatureFlagKeys.AuditLogViewer)]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Audit entries, newest first, filtered by admin, entity type, action and date range, with the total match count
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuditLogPageDto>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null)
    {
        try
        {
            return Ok(await _auditLogService.SearchAsync(new AuditLogQuery
            {
                Page = page,
                PageSize = pageSize,
                EntityType = entityType,
                UserId = userId,
                Action = action,
                From = from?.UtcDateTime,
                To = to?.UtcDateTime,
            }));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// The entity types and admins present in the log, for the filter choices
    /// </summary>
    [HttpGet("filters")]
    [ProducesResponseType(typeof(AuditLogFiltersDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogFiltersDto>> GetFilters() => Ok(await _auditLogService.GetFiltersAsync());
}
