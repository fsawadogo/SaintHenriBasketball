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
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public QrCheckInController(IQrCheckInService qrService, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _qrService = qrService;
        _configuration = configuration;
        _environment = environment;
    }

    /// The printed QR code must open the club's own site. A caller-supplied URL is used only when it is
    /// that site, or a localhost preview during development.
    private string CheckInBaseUrl(string? frontendUrl)
    {
        var appUrl = _configuration["AppUrl"]?.TrimEnd('/');
        if (Uri.TryCreate(frontendUrl, UriKind.Absolute, out var requested) && requested.Scheme is "http" or "https")
        {
            var origin = requested.GetLeftPart(UriPartial.Authority);
            if (appUrl != null && string.Equals(origin, appUrl, StringComparison.OrdinalIgnoreCase)) return origin;
            if (_environment.IsDevelopment() && requested.IsLoopback) return origin;
        }
        return appUrl ?? $"{Request.Scheme}://{Request.Host}";
    }

    [HttpGet("api/v{version:apiVersion}/sessions/{sessionId:guid}/qr-token")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SessionQrTokenDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionQrTokenDto>> GetToken(Guid sessionId, [FromQuery] string? frontendUrl = null)
    {
        try
        {
            var token = await _qrService.GenerateTokenAsync(sessionId, CheckInBaseUrl(frontendUrl));
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
