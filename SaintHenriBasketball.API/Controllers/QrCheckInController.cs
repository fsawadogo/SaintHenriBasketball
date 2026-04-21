using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.QrCheckIn;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
[RequireFeature(FeatureFlagKeys.QrCheckIn)]
public class QrCheckInController : BaseApiController
{
    private readonly IQrCheckInService _qrService;

    public QrCheckInController(IQrCheckInService qrService)
    {
        _qrService = qrService;
    }

    [HttpGet("api/v{version:apiVersion}/sessions/{sessionId:guid}/qr-token")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SessionQrTokenDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionQrTokenDto>> GetToken(Guid sessionId, [FromQuery] string? frontendUrl = null)
    {
        try
        {
            var baseUrl = !string.IsNullOrEmpty(frontendUrl) ? frontendUrl : $"{Request.Scheme}://{Request.Host}";
            var token = await _qrService.GenerateTokenAsync(sessionId, baseUrl);
            return Ok(token);
        }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("api/v{version:apiVersion}/sessions/check-in")]
    [ProducesResponseType(typeof(QrCheckInResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QrCheckInResultDto>> CheckIn([FromBody] QrCheckInRequestDto body)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var result = await _qrService.CheckInAsync(userId.Value, body.Token);
            return Ok(result);
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }
}
