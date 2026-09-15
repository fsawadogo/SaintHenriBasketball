using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.PlayerTimeline;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize(Roles = "Admin")]
[RequireFeature(FeatureFlagKeys.PlayerTimeline)]
public class PlayerTimelineController : BaseApiController
{
    private readonly IPlayerTimelineService _timeline;

    public PlayerTimelineController(IPlayerTimelineService timeline)
    {
        _timeline = timeline;
    }

    /// <summary>
    /// Everything that happened with a player, newest first, with counts per event type and the admin notes (Admin only).
    /// Filter with types=payment,credit (or repeat types=). Works for deactivated and anonymized players.
    /// </summary>
    [HttpGet("api/v{version:apiVersion}/admin/users/{userId:guid}/timeline")]
    [ProducesResponseType(typeof(PlayerTimelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerTimelineDto>> Get(
        Guid userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string[]? types = null)
    {
        try
        {
            return Ok(await _timeline.GetTimelineAsync(userId, new PlayerTimelineQuery { Page = page, PageSize = pageSize, Types = types }));
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}
