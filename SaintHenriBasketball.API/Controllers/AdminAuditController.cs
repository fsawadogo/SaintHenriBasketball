using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/audit")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminAuditController(IAuditLogService auditLogService) : ControllerBase
{
    public const int MaxWhatLength = 60;
    public const int MaxFiltersLength = 500;

    /// <summary>
    /// Records that an admin downloaded an export. Export files are built in the browser, so the app reports them here.
    /// </summary>
    [HttpPost("export")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LogExport([FromBody] ExportAuditRequest request)
    {
        var what = request.What?.Trim();
        if (string.IsNullOrEmpty(what) || what.Length > MaxWhatLength)
            return BadRequest($"Name what was exported in {MaxWhatLength} characters or fewer.");
        if (request.RowCount < 0)
            return BadRequest("The row count can't be negative.");

        var filters = request.Filters?.Trim();
        if (filters is { Length: > MaxFiltersLength }) filters = filters[..MaxFiltersLength];
        await auditLogService.LogAsync("Exported", what, null,
            $"Exported {request.RowCount} row(s)" + (string.IsNullOrEmpty(filters) ? "" : $". Filters: {filters}"),
            User.AuditUserId(), User.AuditUserName());
        return NoContent();
    }
}

public class ExportAuditRequest
{
    /// What was exported, e.g. "Players" or "Payments".
    public string? What { get; set; }
    public int RowCount { get; set; }
    /// The filters in effect, in plain words.
    public string? Filters { get; set; }
}
