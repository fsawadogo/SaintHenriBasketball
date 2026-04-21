using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.Stats;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
[RequireFeature(FeatureFlagKeys.PersonalStats)]
public class StatsController : BaseApiController
{
    private readonly IPersonalStatsService _statsService;

    public StatsController(IPersonalStatsService statsService)
    {
        _statsService = statsService;
    }

    [HttpGet("api/v{version:apiVersion}/users/me/stats")]
    [ProducesResponseType(typeof(PersonalStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonalStatsDto>> GetMyStats()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var stats = await _statsService.GetForUserAsync(userId.Value);
            return Ok(stats);
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}
