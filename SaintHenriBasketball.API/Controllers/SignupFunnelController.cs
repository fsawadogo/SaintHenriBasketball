using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using SaintHenriBasketball.Application.DTOs.SignupFunnel;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Helpers;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

/// <summary>
/// Where new players stop: signed up, confirmed their email, reserved, paid, played.
/// Hidden (404) while the signup-funnel flag is off.
/// </summary>
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/admin/reports/signup-funnel")]
[Authorize(Policy = StaffAccess.TreasurerOrAdminPolicy)]
[RequireFeature(FeatureFlagKeys.SignupFunnel)]
public class SignupFunnelController : ControllerBase
{
    private readonly ISignupFunnelService _service;

    public SignupFunnelController(ISignupFunnelService service)
    {
        _service = service;
    }

    /// <summary>Counts for the players who signed up in the period; the last 90 days by default.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(SignupFunnelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SignupFunnelDto>> Get([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
    {
        try { return Ok(await _service.GetAsync(from, to)); }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
    }
}
