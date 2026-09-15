using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.SeasonRollover;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// Season rollover: preview and create the next season from a source season, then invite its players to renew.
/// Preview and create take the SOURCE season in the route; invite takes the NEW season in the route and the
/// source season in the body.
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/seasons")]
[Authorize(Roles = "Admin")]
[RequireFeature(FeatureFlagKeys.SeasonRollover)]
public class SeasonRolloverController : BaseApiController
{
    private readonly ISeasonRolloverService _rollover;

    public SeasonRolloverController(ISeasonRolloverService rollover)
    {
        _rollover = rollover;
    }

    /// <summary>Proposes the season after the source season, with its sessions and conflicts. Writes nothing.</summary>
    [HttpPost("{seasonId:guid}/rollover/preview")]
    [ProducesResponseType(typeof(SeasonRolloverPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonRolloverPreviewDto>> Preview(Guid seasonId, [FromBody] SeasonRolloverRequestDto? body)
    {
        try { return Ok(await _rollover.PreviewAsync(seasonId, body)); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Creates the proposed season as a draft (Closed, not current) with its Saturday sessions.</summary>
    [HttpPost("{seasonId:guid}/rollover")]
    [ProducesResponseType(typeof(SeasonRolloverDraftResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonRolloverDraftResultDto>> CreateDraft(Guid seasonId, [FromBody] SeasonRolloverRequestDto? body)
    {
        try { return StatusCode(StatusCodes.Status201Created, await _rollover.CreateDraftAsync(seasonId, body, GetUserId(), AdminName())); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Emails the source season's players an invitation to renew for this (new) season.</summary>
    [HttpPost("{seasonId:guid}/rollover/invite")]
    [ProducesResponseType(typeof(SeasonRenewalInviteResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonRenewalInviteResultDto>> Invite(Guid seasonId, [FromBody] SeasonRenewalInviteRequestDto body)
    {
        try { return Ok(await _rollover.SendRenewalInvitesAsync(seasonId, body, GetUserId(), AdminName())); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    private string AdminName() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "Admin";
}
