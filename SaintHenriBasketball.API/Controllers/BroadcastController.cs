using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.Broadcast;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

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

    [HttpPost("send")]
    [ProducesResponseType(typeof(SendBroadcastResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SendBroadcastResultDto>> Send([FromBody] SendBroadcastRequestDto body)
    {
        try
        {
            var result = await _broadcastService.SendAsync(body);
            return Ok(result);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }
}
