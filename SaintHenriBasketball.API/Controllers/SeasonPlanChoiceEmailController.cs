using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.API.Controllers;

/// <summary>
/// The "choose your plan" email a week before a season starts. A daily job sends it; these endpoints
/// are for seeing what it will do, reading it first, and sending it by hand if a season is set up late.
/// Hidden (404) while the season-plan-choice-email flag is off.
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/seasons/plan-choice-email")]
[Authorize(Roles = "Admin")]
[RequireFeature(FeatureFlagKeys.SeasonPlanChoiceEmail)]
public class SeasonPlanChoiceEmailController : ControllerBase
{
    private readonly ISeasonPlanChoiceEmailService _service;

    public SeasonPlanChoiceEmailController(ISeasonPlanChoiceEmailService service)
    {
        _service = service;
    }

    /// <summary>Who would be emailed if the job ran now, without sending anything.</summary>
    [HttpGet("preview")]
    [ProducesResponseType(typeof(PlanChoiceSendResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlanChoiceSendResultDto>> Preview([FromQuery] Guid? seasonId, [FromQuery] int daysAhead = 7) =>
        Ok(seasonId is Guid id
            ? await _service.RunForSeasonAsync(id, dryRun: true)
            : await _service.RunForSeasonStartingInAsync(daysAhead, dryRun: true));

    /// <summary>The email itself, as a player would read it.</summary>
    [HttpGet("{seasonId:guid}/html")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Html(Guid seasonId, [FromQuery] EmailLanguage language = EmailLanguage.French)
    {
        try { return Content(await _service.PreviewAsync(seasonId, language), "text/html"); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Sends it now. Players who already received it for this season are skipped.</summary>
    [HttpPost("send")]
    [ProducesResponseType(typeof(PlanChoiceSendResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanChoiceSendResultDto>> Send([FromQuery] Guid? seasonId, [FromQuery] int daysAhead = 7)
    {
        try
        {
            return Ok(seasonId is Guid id
                ? await _service.RunForSeasonAsync(id)
                : await _service.RunForSeasonStartingInAsync(daysAhead));
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}
