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
[RequireFeature(FeatureFlagKeys.StreaksBadges)]
public class BadgesController : BaseApiController
{
    private readonly IPersonalStatsService _statsService;

    public BadgesController(IPersonalStatsService statsService)
    {
        _statsService = statsService;
    }

    [HttpGet("api/v{version:apiVersion}/users/me/badges")]
    [ProducesResponseType(typeof(BadgesSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BadgesSummaryDto>> GetMyBadges()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var summary = await _statsService.GetBadgesSummaryAsync(userId.Value);
            return Ok(summary);
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}
