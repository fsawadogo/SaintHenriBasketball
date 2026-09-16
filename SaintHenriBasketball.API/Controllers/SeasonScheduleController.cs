using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.SeasonSchedule;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/seasons/schedule")]
[Authorize(Roles = "Admin")]
[RequireFeature(FeatureFlagKeys.SeasonScheduleWizard)]
public class SeasonScheduleController : BaseApiController
{
    private readonly ISeasonScheduleService _schedule;

    public SeasonScheduleController(ISeasonScheduleService schedule)
    {
        _schedule = schedule;
    }

    /// The sessions the wizard would create. Writes nothing.
    [HttpPost("preview")]
    [SkipAdminAudit]
    [ProducesResponseType(typeof(SeasonSchedulePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeasonSchedulePreviewDto>> Preview([FromBody] SeasonSchedulePreviewRequestDto body)
    {
        try { return Ok(await _schedule.PreviewAsync(body)); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    /// Creates the season and its sessions together. The service writes its own activity log entry.
    [HttpPost]
    [SkipAdminAudit]
    [ProducesResponseType(typeof(SeasonScheduleCreateResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeasonScheduleCreateResultDto>> Create([FromBody] CreateSeasonWithScheduleDto body)
    {
        try { return StatusCode(StatusCodes.Status201Created, await _schedule.CreateAsync(body, GetUserId(), AdminName())); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }

    private string AdminName() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "Admin";
}
