using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaintHenriBasketball.API.Filters;
using Microsoft.Extensions.Configuration;
using SaintHenriBasketball.Application.DTOs.Referrals;
using SaintHenriBasketball.Application.Exceptions;
using SaintHenriBasketball.Application.FeatureFlags;
using SaintHenriBasketball.Application.Services.Interfaces;

namespace SaintHenriBasketball.API.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Authorize]
[RequireFeature(FeatureFlagKeys.Referrals)]
public class ReferralsController : BaseApiController
{
    private readonly IReferralService _referralService;
    private readonly IConfiguration _configuration;

    public ReferralsController(IReferralService referralService, IConfiguration configuration)
    {
        _referralService = referralService;
        _configuration = configuration;
    }

    [HttpGet("api/v{version:apiVersion}/users/me/referral-code")]
    [ProducesResponseType(typeof(ReferralCodeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReferralCodeDto>> GetOwn()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        // Use the public-facing site URL so referral links point at the frontend (/register),
        // not whatever host this API is deployed on (e.g. azurewebsites.net).
        var baseUrl = _configuration["AppUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var code = await _referralService.GetOrCreateOwnCodeAsync(userId.Value, baseUrl);
        return Ok(code);
    }

    [HttpPost("api/v{version:apiVersion}/users/me/referral-code/redeem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Redeem([FromBody] RedeemReferralCodeDto body)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        try
        {
            await _referralService.RedeemAsync(userId.Value, body.Code);
            return NoContent();
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("api/v{version:apiVersion}/admin/referrals")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<ReferralRedemptionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReferralRedemptionDto>>> GetRedemptions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var items = await _referralService.GetRedemptionsAsync(page, pageSize);
        return Ok(items);
    }

    [HttpPut("api/v{version:apiVersion}/admin/referrals/{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateRedemptionStatusDto body)
    {
        try
        {
            await _referralService.UpdateRedemptionStatusAsync(id, body.RewardStatus);
            return NoContent();
        }
        catch (ValidationException ex) { return BadRequest(ex.Message); }
        catch (NotFoundException ex) { return NotFound(ex.Message); }
    }
}
