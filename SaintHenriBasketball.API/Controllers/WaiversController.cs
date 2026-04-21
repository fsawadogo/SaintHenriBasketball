using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.Waivers;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[RequireFeature(FeatureFlagKeys.Waiver)]
public class WaiversController : BaseApiController
{
    private readonly IWaiverService _waiverService;

    public WaiversController(IWaiverService waiverService)
    {
        _waiverService = waiverService;
    }

    [HttpGet("api/v{version:apiVersion}/waivers/current")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentWaiverDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentWaiverDto>> GetCurrent()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var current = await _waiverService.GetCurrentAsync(userId.Value);
        return Ok(current);
    }

    [HttpPost("api/v{version:apiVersion}/waivers/accept")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Accept()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        try
        {
            await _waiverService.AcceptCurrentAsync(userId.Value, ip);
            return NoContent();
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("api/v{version:apiVersion}/admin/waivers")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<WaiverTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WaiverTemplateDto>>> GetAll()
    {
        var all = await _waiverService.GetAllTemplatesAsync();
        return Ok(all);
    }

    [HttpPost("api/v{version:apiVersion}/admin/waivers")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(WaiverTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WaiverTemplateDto>> Create([FromBody] CreateWaiverTemplateDto body)
    {
        try
        {
            var template = await _waiverService.CreateTemplateAsync(body);
            return CreatedAtAction(nameof(GetAll), new { version = "1.0" }, template);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }
}
