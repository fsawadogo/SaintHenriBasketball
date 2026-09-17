using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.Season;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// How a player pays for the season that is open now.
///
/// Its own controller rather than a route on SeasonsController: this is player-facing, gated by its
/// own flag, and has nothing to do with administering seasons.
[ApiVersion("1.0")]
[ApiController]
[Authorize]
[RequireFeature(FeatureFlagKeys.SeasonPlanChoice)]
public class SeasonPlanController : BaseApiController
{
    private readonly ISeasonPlanService _seasonPlan;
    private readonly ILogger<SeasonPlanController> _logger;

    public SeasonPlanController(ISeasonPlanService seasonPlan, ILogger<SeasonPlanController> logger)
    {
        _seasonPlan = seasonPlan;
        _logger = logger;
    }

    /// The open season and this player's standing in it.
    /// 204 when no season is open — there is nothing to choose, so the prompt must not appear.
    [HttpGet("api/v{version:apiVersion}/seasons/plan/current")]
    [ProducesResponseType(typeof(SeasonPlanStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SeasonPlanStateDto>> GetCurrent()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var state = await _seasonPlan.GetCurrentAsync(userId.Value);
        return state is null ? NoContent() : Ok(state);
    }

    /// Records this player's plan for the open season.
    ///
    /// 409 — not 400 — when the pass is sold out: it is a state collision, not a malformed request,
    /// and the client has to tell "someone took the last spot while you were deciding" apart from
    /// "you sent nonsense".
    [HttpPost("api/v{version:apiVersion}/seasons/plan/current")]
    [ProducesResponseType(typeof(SeasonPlanStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SeasonPlanStateDto>> Choose([FromBody] ChooseSeasonPlanDto body)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            return Ok(await _seasonPlan.ChooseAsync(userId.Value, body.Plan));
        }
        catch (ValidationException ex)
        {
            _logger.LogInformation("Plan choice refused for user {UserId}: {Message}", userId, ex.Message);
            return Conflict(ex.Message);
        }
    }
}
