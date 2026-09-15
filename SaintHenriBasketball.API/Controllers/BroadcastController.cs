using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.Broadcast;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;
using System.Security.Claims;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/broadcasts")]
[Authorize(Roles = "Admin")]
[RequireFeature(FeatureFlagKeys.AdminBroadcast)]
public class BroadcastController : BaseApiController
{
    private readonly IBroadcastService _broadcastService;

    public BroadcastController(IBroadcastService broadcastService)
    {
        _broadcastService = broadcastService;
    }

    [HttpGet("preview")]
    [ProducesResponseType(typeof(BroadcastAudiencePreviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BroadcastAudiencePreviewDto>> Preview([FromQuery] BroadcastAudience audience)
    {
        var preview = await _broadcastService.PreviewAudienceAsync(audience);
        return Ok(preview);
    }

    /// <summary>
    /// Sent and sending broadcasts, newest first, with the total count
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(BroadcastHistoryPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BroadcastHistoryPageDto>> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await _broadcastService.GetHistoryAsync(page, pageSize));

    /// <summary>
    /// One broadcast with its full message
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BroadcastDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BroadcastDetailDto>> GetBroadcast(Guid id)
    {
        try { return Ok(await _broadcastService.GetBroadcastAsync(id)); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    // Recorded in the broadcast history now and in the audit log once the background delivery finishes.
    [SkipAdminAudit]
    [HttpPost("send")]
    [ProducesResponseType(typeof(SendBroadcastResultDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SendBroadcastResultDto>> Send([FromBody] SendBroadcastRequestDto body)
    {
        try
        {
            var adminName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "Admin";
            var result = await _broadcastService.QueueAsync(body, GetUserId(), adminName);
            return Accepted(result);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }
}
