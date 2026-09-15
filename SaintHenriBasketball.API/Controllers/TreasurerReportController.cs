using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Extensions;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.TreasurerReport;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// <summary>
/// Treasurer report: money collected and outstanding, by month (America/Toronto), season and overall.
/// Give either <c>from</c> and <c>to</c> (ISO 8601 instants, both inclusive, at most 3 years apart) or <c>seasonId</c>.
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/reports/treasurer")]
[Authorize(Policy = StaffAccess.TreasurerOrAdminPolicy)]
[RequireFeature(FeatureFlagKeys.TreasurerReport)]
public class TreasurerReportController : BaseApiController
{
    private readonly ITreasurerReportService _treasurerReportService;

    public TreasurerReportController(ITreasurerReportService treasurerReportService)
    {
        _treasurerReportService = treasurerReportService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(TreasurerReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TreasurerReportDto>> GetReport(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? seasonId)
    {
        try
        {
            return Ok(await _treasurerReportService.GetReportAsync(ToQuery(from, to, seasonId)));
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>The same report as a CSV download (UTF-8 with BOM). Each download is recorded in the audit log.</summary>
    // No [Produces("text/csv")]: it would push the 400/404 messages through CSV content negotiation.
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Export(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? seasonId)
    {
        try
        {
            var export = await _treasurerReportService.ExportCsvAsync(ToQuery(from, to, seasonId), User.AuditUserId(), User.AuditUserName());
            return File(export.Content, "text/csv; charset=utf-8", export.FileName);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    private static TreasurerReportQuery ToQuery(DateTimeOffset? from, DateTimeOffset? to, Guid? seasonId) => new()
    {
        From = from?.UtcDateTime,
        To = to?.UtcDateTime,
        SeasonId = seasonId,
    };
}
